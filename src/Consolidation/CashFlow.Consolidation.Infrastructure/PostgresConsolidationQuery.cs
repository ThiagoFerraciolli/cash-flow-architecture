using System.Data;
using CashFlow.Consolidation.Application;
using Npgsql;

namespace CashFlow.Consolidation.Infrastructure;

public sealed class PostgresConsolidationQuery(NpgsqlDataSource source) : IConsolidationQuery
{
    public async Task<ConsolidationPage> GetAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        await using var connection = await source.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        Guid generation;
        bool maintenance;
        await using (var control = new NpgsqlCommand(
            "SELECT active_generation_id, maintenance AND maintenance_expires_at > clock_timestamp() FROM projection_control WHERE singleton = true",
            connection, transaction))
        await using (var reader = await control.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
                throw new InvalidOperationException("Projection control is missing.");
            generation = reader.GetGuid(0);
            maintenance = reader.GetBoolean(1);
        }

        var days = new List<DailyConsolidation>();
        await using (var command = new NpgsqlCommand("""
            SELECT business_date, total_credits, total_debits, transaction_count, last_updated_at
            FROM daily_consolidations
            WHERE generation_id = $1 AND business_date >= $2 AND business_date <= $3
            ORDER BY business_date
            """, connection, transaction))
        {
            command.Parameters.AddWithValue(generation);
            command.Parameters.AddWithValue(from);
            command.Parameters.AddWithValue(to);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                days.Add(new DailyConsolidation(reader.GetFieldValue<DateOnly>(0), reader.GetDecimal(1),
                    reader.GetDecimal(2), reader.GetInt64(3), reader.GetFieldValue<DateTimeOffset>(4)));
        }
        await transaction.CommitAsync(cancellationToken);
        return new ConsolidationPage(generation, maintenance, days);
    }

    public async Task ProbeAsync(CancellationToken cancellationToken)
    {
        await using var command = source.CreateCommand("""
            SELECT 1 FROM projection_control c
            JOIN projection_generations g ON g.generation_id = c.active_generation_id
            WHERE c.singleton = true AND g.status = 'ACTIVE'
              AND to_regclass('daily_consolidations') IS NOT NULL
            """);
        if (await command.ExecuteScalarAsync(cancellationToken) is null)
            throw new InvalidOperationException("Active projection schema is not ready.");
    }
}
