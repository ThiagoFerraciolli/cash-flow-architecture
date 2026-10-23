using System.Globalization;
using System.Text;
using System.Text.Json;
using CashFlow.Consolidation.Application;
using CashFlow.Consolidation.Infrastructure;
using Npgsql;
using Xunit;

namespace CashFlow.Consolidation.IntegrationTests;

public sealed class ProjectionTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Credit_creates_daily_total_and_derived_balance()
    {
        await WithDatabase(async source =>
        {
            var projection = new PostgresTransactionProjection(source);
            Assert.Equal(ApplyTransactionResult.Applied, await projection.ApplyAsync(Fact(), Token));
            Assert.Equal((100m, 0m, 1L), await Daily(source, "2026-10-21"));
            var (credits, debits, _) = await Daily(source, "2026-10-21");
            Assert.Equal(100m, credits - debits);
        });
    }

    [Fact]
    public async Task Debit_creates_debit_total()
    {
        await WithDatabase(async source =>
        {
            await new PostgresTransactionProjection(source).ApplyAsync(Fact(kind: "DEBIT", amount: "20.00"), Token);
            Assert.Equal((0m, 20m, 1L), await Daily(source, "2026-10-21"));
        });
    }

    [Fact]
    public async Task Multiple_transactions_accumulate_by_day_and_other_days_remain_independent()
    {
        await WithDatabase(async source =>
        {
            var projection = new PostgresTransactionProjection(source);
            await projection.ApplyAsync(Fact(amount: "100.00"), Token);
            await projection.ApplyAsync(Fact(kind: "DEBIT", amount: "20.00"), Token);
            await projection.ApplyAsync(Fact(amount: "50.00"), Token);
            await projection.ApplyAsync(Fact(date: "2026-10-22", amount: "7.00"), Token);
            Assert.Equal((150m, 20m, 3L), await Daily(source, "2026-10-21"));
            Assert.Equal((7m, 0m, 1L), await Daily(source, "2026-10-22"));
        });
    }

    [Fact]
    public async Task Same_event_and_new_event_same_fact_are_no_ops_without_touching_timestamp()
    {
        await WithDatabase(async source =>
        {
            var projection = new PostgresTransactionProjection(source);
            var original = Fact();
            await projection.ApplyAsync(original, Token);
            var timestamp = await UpdatedAt(source);
            Assert.Equal(ApplyTransactionResult.Duplicate, await projection.ApplyAsync(original, Token));
            Assert.Equal(ApplyTransactionResult.Duplicate,
                await projection.ApplyAsync(Fact(Guid.NewGuid(), original.TransactionId), Token));
            Assert.Equal((100m, 0m, 1L), await Daily(source, "2026-10-21"));
            Assert.Equal(1L, await Count(source, "processed_transactions"));
            Assert.Equal(timestamp, await UpdatedAt(source));
        });
    }

    [Theory]
    [InlineData("101.00", "CREDIT", "2026-10-21")]
    [InlineData("100.00", "DEBIT", "2026-10-21")]
    [InlineData("100.00", "CREDIT", "2026-10-22")]
    public async Task Same_transaction_with_different_financial_fact_is_permanent_conflict(
        string amount, string kind, string date)
    {
        await WithDatabase(async source =>
        {
            var projection = new PostgresTransactionProjection(source);
            var original = Fact();
            await projection.ApplyAsync(original, Token);
            await Assert.ThrowsAsync<FinancialIdentityConflictException>(() => projection.ApplyAsync(
                Fact(Guid.NewGuid(), original.TransactionId, kind, amount, date), Token));
            Assert.Equal((100m, 0m, 1L), await Daily(source, "2026-10-21"));
            Assert.Equal(1L, await Count(source, "processed_transactions"));
        });
    }

    [Fact]
    public async Task Event_id_reused_for_another_transaction_is_permanent_conflict()
    {
        await WithDatabase(async source =>
        {
            var projection = new PostgresTransactionProjection(source);
            var original = Fact();
            await projection.ApplyAsync(original, Token);
            await Assert.ThrowsAsync<FinancialIdentityConflictException>(() => projection.ApplyAsync(
                Fact(original.EventId, Guid.NewGuid()), Token));
            Assert.Equal(1L, await Count(source, "processed_transactions"));
            Assert.Equal((100m, 0m, 1L), await Daily(source, "2026-10-21"));
        });
    }

    [Theory]
    [InlineData("{", "malformed")]
    [InlineData("{\"schemaVersion\":2}", "version")]
    [InlineData("{\"schemaVersion\":1,\"eventType\":\"Other\"}", "type")]
    public void Invalid_envelope_is_rejected(string payload, string _)
    {
        Assert.Throws<InvalidTransactionEventException>(() =>
            TransactionCreatedFact.Parse(Encoding.UTF8.GetBytes(payload)));
    }

    [Theory]
    [InlineData("0.00")]
    [InlineData("1.001")]
    [InlineData("1000000000.00")]
    [InlineData("1e2")]
    public void Invalid_amount_is_rejected(string amount)
    {
        Assert.Throws<InvalidTransactionEventException>(() => ParsePayload(Guid.NewGuid(), Guid.NewGuid(),
            "CREDIT", amount, "2026-10-21"));
    }

    [Fact]
    public void Technical_event_metadata_does_not_change_fact_hash()
    {
        var transaction = Guid.NewGuid();
        var first = Fact(Guid.NewGuid(), transaction);
        var second = Fact(Guid.NewGuid(), transaction);
        Assert.Equal(first.FactHash, second.FactHash);
    }

    [Fact]
    public async Task Concurrent_duplicates_have_one_financial_effect()
    {
        await WithDatabase(async source =>
        {
            var projection = new PostgresTransactionProjection(source);
            var fact = Fact();
            var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
                projection.ApplyAsync(Fact(Guid.NewGuid(), fact.TransactionId), Token)));
            Assert.Single(results, result => result == ApplyTransactionResult.Applied);
            Assert.Equal(7, results.Count(result => result == ApplyTransactionResult.Duplicate));
            Assert.Equal((100m, 0m, 1L), await Daily(source, "2026-10-21"));
        });
    }

    [Fact]
    public async Task Concurrent_distinct_transactions_accumulate_exactly()
    {
        await WithDatabase(async source =>
        {
            var projection = new PostgresTransactionProjection(source);
            await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => projection.ApplyAsync(Fact(), Token)));
            Assert.Equal((1600m, 0m, 16L), await Daily(source, "2026-10-21"));
            Assert.Equal(16L, await Count(source, "processed_transactions"));
        });
    }

    [Fact]
    public async Task Maintenance_prevents_new_financial_effect()
    {
        await WithDatabase(async source =>
        {
            await using var command = source.CreateCommand("UPDATE projection_control SET maintenance = true");
            await command.ExecuteNonQueryAsync(Token);
            await Assert.ThrowsAsync<ProjectionInMaintenanceException>(() =>
                new PostgresTransactionProjection(source).ApplyAsync(Fact(), Token));
            Assert.Equal(0L, await Count(source, "processed_transactions"));
        });
    }

    [Fact]
    public async Task Readiness_requires_the_complete_projection_schema()
    {
        await WithDatabase(async source =>
        {
            var projection = new PostgresTransactionProjection(source);
            await projection.ProbeAsync(Token);
            await using var command = source.CreateCommand("DROP TABLE daily_consolidations");
            await command.ExecuteNonQueryAsync(Token);
            await Assert.ThrowsAsync<InvalidOperationException>(() => projection.ProbeAsync(Token));
        });
    }

    [Fact]
    public async Task Active_generation_controls_where_new_effect_is_written()
    {
        await WithDatabase(async source =>
        {
            var generation = Guid.NewGuid();
            await using (var insert = source.CreateCommand("INSERT INTO projection_generations (generation_id, status) VALUES ($1, 'ACTIVE')"))
            {
                insert.Parameters.AddWithValue(generation);
                await insert.ExecuteNonQueryAsync(Token);
            }
            await using (var update = source.CreateCommand("UPDATE projection_control SET active_generation_id = $1"))
            {
                update.Parameters.AddWithValue(generation);
                await update.ExecuteNonQueryAsync(Token);
            }
            await new PostgresTransactionProjection(source).ApplyAsync(Fact(), Token);
            await using var inspect = source.CreateCommand("SELECT generation_id FROM processed_transactions");
            Assert.Equal(generation, await inspect.ExecuteScalarAsync(Token));
        });
    }

    [Fact]
    public async Task Commit_before_ack_then_redelivery_does_not_duplicate_effect()
    {
        await WithDatabase(async source =>
        {
            var fact = Fact();
            await new PostgresTransactionProjection(source).ApplyAsync(fact, Token);
            // The first caller has committed but its transport acknowledgement is lost.
            Assert.Equal(ApplyTransactionResult.Duplicate,
                await new PostgresTransactionProjection(source).ApplyAsync(fact, Token));
            Assert.Equal((100m, 0m, 1L), await Daily(source, "2026-10-21"));
        });
    }

    [Fact]
    public async Task Aggregate_overflow_rolls_back_processed_identity()
    {
        await WithDatabase(async source =>
        {
            var generation = await ActiveGeneration(source);
            await using (var seed = source.CreateCommand("""
                INSERT INTO daily_consolidations (generation_id, business_date, total_credits, total_debits, transaction_count)
                VALUES ($1, DATE '2026-10-21', 99999999999999999999999999.99, 0, 1)
                """))
            {
                seed.Parameters.AddWithValue(generation);
                await seed.ExecuteNonQueryAsync(Token);
            }
            await Assert.ThrowsAsync<PostgresException>(() =>
                new PostgresTransactionProjection(source).ApplyAsync(Fact(), Token));
            Assert.Equal(0L, await Count(source, "processed_transactions"));
        });
    }

    [Fact]
    public async Task Projection_remains_after_new_database_connection()
    {
        await WithDatabase(async source =>
        {
            await new PostgresTransactionProjection(source).ApplyAsync(Fact(), Token);
            var reopenedConnection = new NpgsqlConnectionStringBuilder(
                Environment.GetEnvironmentVariable("CASHFLOW_TEST_CONSOLIDATION_POSTGRES"))
            {
                SearchPath = new NpgsqlConnectionStringBuilder(source.ConnectionString).SearchPath
            }.ConnectionString;
            await using var reopened = NpgsqlDataSource.Create(reopenedConnection);
            Assert.Equal((100m, 0m, 1L), await Daily(reopened, "2026-10-21"));
        });
    }

    private static TransactionCreatedFact Fact(Guid? eventId = null, Guid? transactionId = null,
        string kind = "CREDIT", string amount = "100.00", string date = "2026-10-21") =>
        ParsePayload(eventId ?? Guid.NewGuid(), transactionId ?? Guid.NewGuid(), kind, amount, date);

    private static TransactionCreatedFact ParsePayload(Guid eventId, Guid transactionId,
        string kind, string amount, string date)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 1,
            eventId,
            eventType = "TransactionCreated",
            eventOccurredAt = "2026-10-22T12:00:00Z",
            transactionId,
            transactionType = kind,
            amount,
            businessDate = date
        });
        return TransactionCreatedFact.Parse(payload);
    }

    internal static async Task<(decimal Credits, decimal Debits, long Count)> Daily(NpgsqlDataSource source, string date)
    {
        await using var command = source.CreateCommand("""
            SELECT total_credits, total_debits, transaction_count FROM daily_consolidations
            WHERE business_date = $1
            """);
        command.Parameters.AddWithValue(DateOnly.Parse(date, CultureInfo.InvariantCulture));
        await using var reader = await command.ExecuteReaderAsync(Token);
        Assert.True(await reader.ReadAsync(Token));
        return (reader.GetDecimal(0), reader.GetDecimal(1), reader.GetInt64(2));
    }

    internal static async Task<long> Count(NpgsqlDataSource source, string table)
    {
        await using var command = source.CreateCommand($"SELECT count(*) FROM {table}");
        return (long)(await command.ExecuteScalarAsync(Token))!;
    }

    private static async Task<DateTime> UpdatedAt(NpgsqlDataSource source)
    {
        await using var command = source.CreateCommand("SELECT last_updated_at FROM daily_consolidations");
        return (DateTime)(await command.ExecuteScalarAsync(Token))!;
    }

    private static async Task<Guid> ActiveGeneration(NpgsqlDataSource source)
    {
        await using var command = source.CreateCommand("SELECT active_generation_id FROM projection_control");
        return (Guid)(await command.ExecuteScalarAsync(Token))!;
    }

    internal static async Task WithDatabase(Func<NpgsqlDataSource, Task> test)
    {
        var connection = Environment.GetEnvironmentVariable("CASHFLOW_TEST_CONSOLIDATION_POSTGRES");
        Assert.False(string.IsNullOrWhiteSpace(connection), "Set CASHFLOW_TEST_CONSOLIDATION_POSTGRES.");
        var schema = "test_" + Guid.NewGuid().ToString("N");
        await using (var admin = NpgsqlDataSource.Create(connection))
        {
            await using var create = admin.CreateCommand($"CREATE SCHEMA {schema}");
            await create.ExecuteNonQueryAsync(Token);
        }
        try
        {
            var scoped = new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema }.ConnectionString;
            await using var source = NpgsqlDataSource.Create(scoped);
            await using var migration = source.CreateCommand(await File.ReadAllTextAsync(
                Path.Combine(AppContext.BaseDirectory, "001_consolidation.sql"), Token));
            await migration.ExecuteNonQueryAsync(Token);
            await test(source);
        }
        finally
        {
            await using var admin = NpgsqlDataSource.Create(connection);
            await using var drop = admin.CreateCommand($"DROP SCHEMA {schema} CASCADE");
            await drop.ExecuteNonQueryAsync(Token);
        }
    }
}
