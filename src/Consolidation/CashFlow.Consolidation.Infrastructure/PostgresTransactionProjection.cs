using System.Data;
using CashFlow.Consolidation.Application;
using Npgsql;

namespace CashFlow.Consolidation.Infrastructure;

public sealed class PostgresTransactionProjection(NpgsqlDataSource source) : ITransactionProjection
{
    public async Task<ApplyTransactionResult> ApplyAsync(TransactionCreatedFact fact,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        await using var connection = await source.OpenConnectionAsync(timeout.Token);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, timeout.Token);
        Guid generation;
        await using (var control = new NpgsqlCommand(
            "SELECT active_generation_id, maintenance AND maintenance_expires_at > clock_timestamp() FROM projection_control WHERE singleton = true FOR SHARE",
            connection, transaction))
        {
            await using var reader = await control.ExecuteReaderAsync(timeout.Token);
            if (!await reader.ReadAsync(timeout.Token)) throw new InvalidOperationException("Projection control is missing.");
            generation = reader.GetGuid(0);
            if (reader.GetBoolean(1)) throw new ProjectionInMaintenanceException();
        }

        await using (var insert = new NpgsqlCommand("""
            INSERT INTO processed_transactions
                (generation_id, transaction_id, event_id, business_date, type, amount, fact_hash)
            VALUES ($1, $2, $3, $4, $5, $6, $7)
            ON CONFLICT DO NOTHING
            """, connection, transaction))
        {
            insert.Parameters.AddWithValue(generation);
            insert.Parameters.AddWithValue(fact.TransactionId);
            insert.Parameters.AddWithValue(fact.EventId);
            insert.Parameters.AddWithValue(fact.BusinessDate);
            insert.Parameters.AddWithValue(fact.TransactionType);
            insert.Parameters.AddWithValue(fact.Amount);
            insert.Parameters.AddWithValue(fact.FactHash);
            if (await insert.ExecuteNonQueryAsync(timeout.Token) == 0)
            {
                await CheckDuplicateAsync(connection, transaction, generation, fact, timeout.Token);
                await transaction.CommitAsync(timeout.Token);
                return ApplyTransactionResult.Duplicate;
            }
        }

        await using (var aggregate = new NpgsqlCommand("""
            INSERT INTO daily_consolidations
                (generation_id, business_date, total_credits, total_debits, transaction_count)
            VALUES ($1, $2, $3, $4, 1)
            ON CONFLICT (generation_id, business_date) DO UPDATE SET
                total_credits = daily_consolidations.total_credits + EXCLUDED.total_credits,
                total_debits = daily_consolidations.total_debits + EXCLUDED.total_debits,
                transaction_count = daily_consolidations.transaction_count + 1,
                last_updated_at = clock_timestamp()
            """, connection, transaction))
        {
            aggregate.Parameters.AddWithValue(generation);
            aggregate.Parameters.AddWithValue(fact.BusinessDate);
            aggregate.Parameters.AddWithValue(fact.TransactionType == "CREDIT" ? fact.Amount : 0m);
            aggregate.Parameters.AddWithValue(fact.TransactionType == "DEBIT" ? fact.Amount : 0m);
            await aggregate.ExecuteNonQueryAsync(timeout.Token);
        }
        await transaction.CommitAsync(timeout.Token);
        return ApplyTransactionResult.Applied;
    }

    private static async Task CheckDuplicateAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid generation, TransactionCreatedFact fact, CancellationToken cancellationToken)
    {
        await using (var byEvent = new NpgsqlCommand("""
            SELECT transaction_id, fact_hash FROM processed_transactions
            WHERE generation_id = $1 AND event_id = $2
            """, connection, transaction))
        {
            byEvent.Parameters.AddWithValue(generation);
            byEvent.Parameters.AddWithValue(fact.EventId);
            await using var reader = await byEvent.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken) &&
                (reader.GetGuid(0) != fact.TransactionId || reader.GetString(1) != fact.FactHash))
                throw new FinancialIdentityConflictException("Event identity conflicts with a processed fact.");
        }
        await using var byTransaction = new NpgsqlCommand("""
            SELECT fact_hash FROM processed_transactions
            WHERE generation_id = $1 AND transaction_id = $2
            """, connection, transaction);
        byTransaction.Parameters.AddWithValue(generation);
        byTransaction.Parameters.AddWithValue(fact.TransactionId);
        var previous = (string?)await byTransaction.ExecuteScalarAsync(cancellationToken);
        if (previous is null) throw new InvalidOperationException("Conflict winner is not visible.");
        if (previous != fact.FactHash)
            throw new FinancialIdentityConflictException("Transaction identity conflicts with a processed fact.");
    }

    public async Task ProbeAsync(CancellationToken cancellationToken)
    {
        await using var command = source.CreateCommand("""
            SELECT 1 FROM projection_control c
            JOIN projection_generations g ON g.generation_id = c.active_generation_id
            WHERE c.singleton = true AND g.status = 'ACTIVE'
              AND (c.maintenance = false OR c.maintenance_expires_at <= clock_timestamp())
              AND to_regclass('daily_consolidations') IS NOT NULL
              AND to_regclass('processed_transactions') IS NOT NULL
            """);
        if (await command.ExecuteScalarAsync(cancellationToken) is null)
            throw new InvalidOperationException("Active projection is not ready.");
    }
}
