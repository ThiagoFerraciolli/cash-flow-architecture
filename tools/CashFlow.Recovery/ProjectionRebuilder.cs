using System.Data;
using System.Globalization;
using System.Text.Json;
using CashFlow.Consolidation.Application;
using Npgsql;
using NpgsqlTypes;

namespace CashFlow.Recovery;

public sealed record RebuildResult(Guid PreviousGenerationId, Guid NewGenerationId,
    long TransactionCount, decimal TotalCredits, decimal TotalDebits);

public sealed class ProjectionRebuilder(NpgsqlDataSource transactions, NpgsqlDataSource consolidation)
{
    public async Task<RebuildResult> RunAsync(CancellationToken token,
        Func<CancellationToken, Task>? beforeActivation = null)
    {
        var runId = Guid.NewGuid();
        var lease = new PostgresRecoveryLease(consolidation);
        var previous = await lease.AcquireAsync(runId, token);
        using var work = CancellationTokenSource.CreateLinkedTokenSource(token);
        using var stopRenewal = new CancellationTokenSource();
        var renewal = RenewUntilStoppedAsync(lease, runId, work, stopRenewal.Token);
        try
        {
            var facts = await ReadSnapshotAsync(work.Token);
            var generation = Guid.NewGuid();
            await using var connection = await consolidation.OpenConnectionAsync(work.Token);
            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, work.Token);
            await using (var create = new NpgsqlCommand("""
                INSERT INTO projection_generations (generation_id, status, snapshot_manifest)
                VALUES ($1, 'BUILDING', $2)
                """, connection, transaction))
            {
                create.Parameters.AddWithValue(generation);
                create.Parameters.AddWithValue(NpgsqlDbType.Jsonb, JsonSerializer.Serialize(new
                {
                    source = "financial_transactions",
                    transactionCount = facts.Count,
                    capturedAt = DateTimeOffset.UtcNow
                }));
                await create.ExecuteNonQueryAsync(work.Token);
            }

            foreach (var fact in facts)
                await InsertFactAsync(connection, transaction, generation, fact, work.Token);

            var expected = facts.GroupBy(fact => fact.BusinessDate).ToDictionary(group => group.Key,
                group => (Credits: group.Where(fact => fact.TransactionType == "CREDIT").Sum(fact => fact.Amount),
                    Debits: group.Where(fact => fact.TransactionType == "DEBIT").Sum(fact => fact.Amount),
                    Count: (long)group.Count()));
            await VerifyAsync(connection, transaction, generation, facts.Count, expected, work.Token);
            if (beforeActivation is not null) await beforeActivation(work.Token);
            stopRenewal.Cancel();
            await renewal;
            work.Token.ThrowIfCancellationRequested();

            await using (var activate = new NpgsqlCommand("""
                UPDATE projection_control
                SET active_generation_id = $1, maintenance = false,
                    maintenance_run_id = NULL, maintenance_started_at = NULL,
                    maintenance_expires_at = NULL
                WHERE singleton = true AND active_generation_id = $2
                  AND maintenance = true AND maintenance_run_id = $3
                  AND maintenance_expires_at > clock_timestamp()
                """, connection, transaction))
            {
                activate.Parameters.AddWithValue(generation);
                activate.Parameters.AddWithValue(previous);
                activate.Parameters.AddWithValue(runId);
                if (await activate.ExecuteNonQueryAsync(work.Token) != 1)
                    throw new InvalidOperationException("Recovery no longer owns the active projection.");
            }
            await using (var retire = new NpgsqlCommand(
                "UPDATE projection_generations SET status = 'INACTIVE' WHERE generation_id = $1",
                connection, transaction))
            {
                retire.Parameters.AddWithValue(previous);
                if (await retire.ExecuteNonQueryAsync(work.Token) != 1)
                    throw new InvalidOperationException("Previous generation status update failed.");
            }
            await using (var complete = new NpgsqlCommand("""
                UPDATE projection_generations SET status = 'ACTIVE', activated_at = clock_timestamp()
                WHERE generation_id = $1
                """, connection, transaction))
            {
                complete.Parameters.AddWithValue(generation);
                if (await complete.ExecuteNonQueryAsync(work.Token) != 1)
                    throw new InvalidOperationException("New generation status update failed.");
            }
            await transaction.CommitAsync(work.Token);
            return new RebuildResult(previous, generation, facts.Count,
                facts.Where(fact => fact.TransactionType == "CREDIT").Sum(fact => fact.Amount),
                facts.Where(fact => fact.TransactionType == "DEBIT").Sum(fact => fact.Amount));
        }
        catch
        {
            stopRenewal.Cancel();
            await renewal;
            await lease.ReleaseAsync(runId, previous, CancellationToken.None);
            throw;
        }
    }

    private static async Task RenewUntilStoppedAsync(PostgresRecoveryLease lease, Guid owner,
        CancellationTokenSource work, CancellationToken stop)
    {
        using var timer = new PeriodicTimer(PostgresRecoveryLease.RenewalInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stop))
            {
                if (!await lease.RenewAsync(owner, stop))
                {
                    work.Cancel();
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        catch
        {
            work.Cancel();
        }
    }

    private async Task<List<TransactionCreatedFact>> ReadSnapshotAsync(CancellationToken token)
    {
        var facts = new List<TransactionCreatedFact>();
        await using var connection = await transactions.OpenConnectionAsync(token);
        await using var snapshot = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, token);
        await using (var readOnly = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, snapshot))
            await readOnly.ExecuteNonQueryAsync(token);
        await using var command = new NpgsqlCommand("""
            SELECT id, creation_event_id, type, amount, business_date, created_at
            FROM financial_transactions ORDER BY id
            """, connection, snapshot);
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token))
        {
            var payload = JsonSerializer.SerializeToUtf8Bytes(new
            {
                schemaVersion = 1,
                eventId = reader.GetGuid(1),
                eventType = "TransactionCreated",
                eventOccurredAt = reader.GetFieldValue<DateTimeOffset>(5).UtcDateTime,
                transactionId = reader.GetGuid(0),
                transactionType = reader.GetString(2),
                amount = reader.GetDecimal(3).ToString("F2", CultureInfo.InvariantCulture),
                businessDate = reader.GetFieldValue<DateOnly>(4).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            });
            facts.Add(TransactionCreatedFact.Parse(payload));
        }
        await reader.CloseAsync();
        await snapshot.CommitAsync(token);
        return facts;
    }

    private static async Task InsertFactAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid generation, TransactionCreatedFact fact, CancellationToken token)
    {
        await using (var processed = new NpgsqlCommand("""
            INSERT INTO processed_transactions
                (generation_id, transaction_id, event_id, business_date, type, amount, fact_hash)
            VALUES ($1, $2, $3, $4, $5, $6, $7)
            """, connection, transaction))
        {
            processed.Parameters.AddWithValue(generation);
            processed.Parameters.AddWithValue(fact.TransactionId);
            processed.Parameters.AddWithValue(fact.EventId);
            processed.Parameters.AddWithValue(fact.BusinessDate);
            processed.Parameters.AddWithValue(fact.TransactionType);
            processed.Parameters.AddWithValue(fact.Amount);
            processed.Parameters.AddWithValue(fact.FactHash);
            await processed.ExecuteNonQueryAsync(token);
        }
        await using var daily = new NpgsqlCommand("""
            INSERT INTO daily_consolidations
                (generation_id, business_date, total_credits, total_debits, transaction_count)
            VALUES ($1, $2, $3, $4, 1)
            ON CONFLICT (generation_id, business_date) DO UPDATE SET
                total_credits = daily_consolidations.total_credits + EXCLUDED.total_credits,
                total_debits = daily_consolidations.total_debits + EXCLUDED.total_debits,
                transaction_count = daily_consolidations.transaction_count + 1,
                last_updated_at = clock_timestamp()
            """, connection, transaction);
        daily.Parameters.AddWithValue(generation);
        daily.Parameters.AddWithValue(fact.BusinessDate);
        daily.Parameters.AddWithValue(fact.TransactionType == "CREDIT" ? fact.Amount : 0m);
        daily.Parameters.AddWithValue(fact.TransactionType == "DEBIT" ? fact.Amount : 0m);
        await daily.ExecuteNonQueryAsync(token);
    }

    private static async Task VerifyAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid generation, int expectedCount,
        IReadOnlyDictionary<DateOnly, (decimal Credits, decimal Debits, long Count)> expected,
        CancellationToken token)
    {
        await using (var count = new NpgsqlCommand(
            "SELECT count(*) FROM processed_transactions WHERE generation_id = $1", connection, transaction))
        {
            count.Parameters.AddWithValue(generation);
            if ((long)(await count.ExecuteScalarAsync(token))! != expectedCount)
                throw new InvalidOperationException("Processed identity count differs from source snapshot.");
        }
        await using var daily = new NpgsqlCommand("""
            SELECT business_date, total_credits, total_debits, transaction_count
            FROM daily_consolidations WHERE generation_id = $1
            """, connection, transaction);
        daily.Parameters.AddWithValue(generation);
        await using var reader = await daily.ExecuteReaderAsync(token);
        var observed = new HashSet<DateOnly>();
        while (await reader.ReadAsync(token))
        {
            var day = reader.GetFieldValue<DateOnly>(0);
            if (!expected.TryGetValue(day, out var value) ||
                reader.GetDecimal(1) != value.Credits || reader.GetDecimal(2) != value.Debits ||
                reader.GetInt64(3) != value.Count)
                throw new InvalidOperationException($"Projection verification failed for {day}.");
            observed.Add(day);
        }
        if (observed.Count != expected.Count)
            throw new InvalidOperationException("Projection day count differs from source snapshot.");
    }
}
