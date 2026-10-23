using CashFlow.Recovery;
using Npgsql;
using Xunit;

namespace CashFlow.Consolidation.IntegrationTests;

public sealed class RecoveryTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Valid_lease_rejects_second_owner_and_only_owner_can_renew_or_release()
    {
        await ProjectionTests.WithDatabase(async source =>
        {
            var lease = new PostgresRecoveryLease(source);
            var first = Guid.NewGuid();
            var second = Guid.NewGuid();
            var previous = await lease.AcquireAsync(first, Token);
            Assert.True(await lease.RenewAsync(first, Token));
            await Assert.ThrowsAsync<InvalidOperationException>(() => lease.AcquireAsync(second, Token));
            Assert.False(await lease.RenewAsync(second, Token));
            Assert.False(await lease.ReleaseAsync(second, previous, Token));
            Assert.True(await MaintenanceAsync(source));
            Assert.True(await lease.ReleaseAsync(first, previous, Token));
            Assert.False(await MaintenanceAsync(source));
        });
    }

    [Fact]
    public async Task Expired_lease_can_be_taken_over_and_old_owner_cannot_renew_or_release()
    {
        await ProjectionTests.WithDatabase(async source =>
        {
            var lease = new PostgresRecoveryLease(source);
            var first = Guid.NewGuid();
            var second = Guid.NewGuid();
            var previous = await lease.AcquireAsync(first, Token);
            await ExpireAsync(source);
            Assert.Equal(previous, await lease.AcquireAsync(second, Token));
            Assert.False(await lease.RenewAsync(first, Token));
            Assert.False(await lease.ReleaseAsync(first, previous, Token));
            Assert.Equal(second, await OwnerAsync(source));
            Assert.True(await lease.ReleaseAsync(second, previous, Token));
        });
    }

    [Fact]
    public async Task Expiry_immediately_before_activation_rolls_back_and_keeps_old_generation()
    {
        await WithSeededRecoveryAsync(async (rebuilder, source) =>
        {
            var original = await ActiveAsync(source);
            await Assert.ThrowsAsync<InvalidOperationException>(() => rebuilder.RunAsync(Token,
                _ => ExpireAsync(source)));
            Assert.Equal(original, await ActiveAsync(source));
            Assert.Equal(1L, await ScalarAsync(source, "SELECT count(*) FROM projection_generations"));
            Assert.True(await MaintenanceAsync(source));
            Assert.False(await EffectiveMaintenanceAsync(source));
        });
    }

    [Fact]
    public async Task Stale_recovery_cannot_activate_after_another_owner_takes_over()
    {
        await WithSeededRecoveryAsync(async (rebuilder, source) =>
        {
            var original = await ActiveAsync(source);
            var newOwner = Guid.NewGuid();
            var lease = new PostgresRecoveryLease(source);
            await Assert.ThrowsAsync<InvalidOperationException>(() => rebuilder.RunAsync(Token, async _ =>
            {
                await ExpireAsync(source);
                Assert.Equal(original, await lease.AcquireAsync(newOwner, Token));
            }));
            Assert.Equal(original, await ActiveAsync(source));
            Assert.Equal(newOwner, await OwnerAsync(source));
            Assert.True(await MaintenanceAsync(source));
            Assert.Equal(1L, await ScalarAsync(source, "SELECT count(*) FROM projection_generations"));
            Assert.True(await lease.ReleaseAsync(newOwner, original, Token));
        });
    }

    [Fact]
    public async Task Failed_rebuild_keeps_active_generation_and_successful_rebuild_matches_source()
    {
        var connectionString = Environment.GetEnvironmentVariable("CASHFLOW_TEST_POSTGRES");
        Assert.False(string.IsNullOrWhiteSpace(connectionString), "Set CASHFLOW_TEST_POSTGRES.");
        var schema = "test_" + Guid.NewGuid().ToString("N");
        await using (var admin = NpgsqlDataSource.Create(connectionString))
        {
            await using var create = admin.CreateCommand($"CREATE SCHEMA {schema}");
            await create.ExecuteNonQueryAsync(Token);
        }
        try
        {
            var scoped = new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = schema }.ConnectionString;
            await using var transactions = NpgsqlDataSource.Create(scoped);
            await using (var migration = transactions.CreateCommand(await File.ReadAllTextAsync(
                Path.Combine(AppContext.BaseDirectory, "001_financial_transactions.sql"), Token)))
                await migration.ExecuteNonQueryAsync(Token);
            await SeedAsync(transactions, "CREDIT", 100m);
            await SeedAsync(transactions, "DEBIT", 20m);

            await ProjectionTests.WithDatabase(async consolidation =>
            {
                var original = await ActiveAsync(consolidation);
                var rebuilder = new ProjectionRebuilder(transactions, consolidation);
                await Assert.ThrowsAsync<InvalidOperationException>(() => rebuilder.RunAsync(Token,
                    _ => throw new InvalidOperationException("Injected failure before activation.")));
                Assert.Equal(original, await ActiveAsync(consolidation));
                Assert.False(await MaintenanceAsync(consolidation));
                Assert.Equal(1L, await ScalarAsync(consolidation,
                    "SELECT count(*) FROM projection_generations"));

                var result = await rebuilder.RunAsync(Token);
                Assert.Equal(original, result.PreviousGenerationId);
                Assert.NotEqual(original, result.NewGenerationId);
                Assert.Equal(result.NewGenerationId, await ActiveAsync(consolidation));
                Assert.Equal(2L, result.TransactionCount);
                Assert.Equal(100m, result.TotalCredits);
                Assert.Equal(20m, result.TotalDebits);
                Assert.Equal(2L, await ScalarAsync(consolidation, """
                    SELECT transaction_count FROM daily_consolidations
                    WHERE generation_id = (SELECT active_generation_id FROM projection_control)
                      AND business_date = DATE '2026-10-21'
                    """));
                Assert.Equal(100m, await DecimalAsync(consolidation, """
                    SELECT total_credits FROM daily_consolidations
                    WHERE generation_id = (SELECT active_generation_id FROM projection_control)
                      AND business_date = DATE '2026-10-21'
                    """));
                Assert.Equal(20m, await DecimalAsync(consolidation, """
                    SELECT total_debits FROM daily_consolidations
                    WHERE generation_id = (SELECT active_generation_id FROM projection_control)
                      AND business_date = DATE '2026-10-21'
                    """));
                Assert.Equal(2L, await ScalarAsync(consolidation,
                    "SELECT count(*) FROM processed_transactions WHERE generation_id = (SELECT active_generation_id FROM projection_control)"));
                Assert.Equal(2L, await ScalarAsync(transactions, "SELECT count(*) FROM financial_transactions"));
            });
        }
        finally
        {
            await using var admin = NpgsqlDataSource.Create(connectionString);
            await using var drop = admin.CreateCommand($"DROP SCHEMA {schema} CASCADE");
            await drop.ExecuteNonQueryAsync(Token);
        }
    }

    private static async Task SeedAsync(NpgsqlDataSource source, string kind, decimal amount)
    {
        await using var command = source.CreateCommand("""
            INSERT INTO financial_transactions
              (id, creation_event_id, type, amount, occurred_at, business_date, created_at)
            VALUES ($1, $2, $3, $4, $5, $6, $7)
            """);
        command.Parameters.AddWithValue(Guid.NewGuid());
        command.Parameters.AddWithValue(Guid.NewGuid());
        command.Parameters.AddWithValue(kind);
        command.Parameters.AddWithValue(amount);
        command.Parameters.AddWithValue(new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero));
        command.Parameters.AddWithValue(new DateOnly(2026, 10, 21));
        command.Parameters.AddWithValue(DateTimeOffset.UtcNow);
        await command.ExecuteNonQueryAsync(Token);
    }

    private static async Task WithSeededRecoveryAsync(Func<ProjectionRebuilder, NpgsqlDataSource, Task> test)
    {
        var connectionString = Environment.GetEnvironmentVariable("CASHFLOW_TEST_POSTGRES");
        Assert.False(string.IsNullOrWhiteSpace(connectionString), "Set CASHFLOW_TEST_POSTGRES.");
        var schema = "test_" + Guid.NewGuid().ToString("N");
        await using (var admin = NpgsqlDataSource.Create(connectionString))
        {
            await using var create = admin.CreateCommand($"CREATE SCHEMA {schema}");
            await create.ExecuteNonQueryAsync(Token);
        }
        try
        {
            var scoped = new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = schema }.ConnectionString;
            await using var transactions = NpgsqlDataSource.Create(scoped);
            await using (var migration = transactions.CreateCommand(await File.ReadAllTextAsync(
                Path.Combine(AppContext.BaseDirectory, "001_financial_transactions.sql"), Token)))
                await migration.ExecuteNonQueryAsync(Token);
            await SeedAsync(transactions, "CREDIT", 100m);
            await ProjectionTests.WithDatabase(async consolidation =>
                await test(new ProjectionRebuilder(transactions, consolidation), consolidation));
        }
        finally
        {
            await using var admin = NpgsqlDataSource.Create(connectionString);
            await using var drop = admin.CreateCommand($"DROP SCHEMA {schema} CASCADE");
            await drop.ExecuteNonQueryAsync(Token);
        }
    }

    private static async Task ExpireAsync(NpgsqlDataSource source)
    {
        await using var command = source.CreateCommand(
            "UPDATE projection_control SET maintenance_expires_at = clock_timestamp() - interval '1 second'");
        await command.ExecuteNonQueryAsync(Token);
    }

    private static async Task<Guid> OwnerAsync(NpgsqlDataSource source)
    {
        await using var command = source.CreateCommand("SELECT maintenance_run_id FROM projection_control");
        return (Guid)(await command.ExecuteScalarAsync(Token))!;
    }

    private static async Task<bool> EffectiveMaintenanceAsync(NpgsqlDataSource source)
    {
        await using var command = source.CreateCommand(
            "SELECT maintenance AND maintenance_expires_at > clock_timestamp() FROM projection_control");
        return (bool)(await command.ExecuteScalarAsync(Token))!;
    }

    private static async Task<Guid> ActiveAsync(NpgsqlDataSource source)
    {
        await using var command = source.CreateCommand(
            "SELECT active_generation_id FROM projection_control WHERE singleton = true");
        return (Guid)(await command.ExecuteScalarAsync(Token))!;
    }

    private static async Task<bool> MaintenanceAsync(NpgsqlDataSource source)
    {
        await using var command = source.CreateCommand(
            "SELECT maintenance FROM projection_control WHERE singleton = true");
        return (bool)(await command.ExecuteScalarAsync(Token))!;
    }

    private static async Task<long> ScalarAsync(NpgsqlDataSource source, string sql)
    {
        await using var command = source.CreateCommand(sql);
        return (long)(await command.ExecuteScalarAsync(Token))!;
    }

    private static async Task<decimal> DecimalAsync(NpgsqlDataSource source, string sql)
    {
        await using var command = source.CreateCommand(sql);
        return (decimal)(await command.ExecuteScalarAsync(Token))!;
    }
}
