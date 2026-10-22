using CashFlow.Transactions.Domain;
using CashFlow.Transactions.Infrastructure;
using Npgsql;
using Xunit;

namespace CashFlow.Transactions.IntegrationTests;

public sealed class PostgresTransactionTests
{
    [Fact]
    public async Task Migration_and_repository_round_trip()
    {
        var connectionString = Environment.GetEnvironmentVariable("CASHFLOW_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) Assert.Skip("Set CASHFLOW_TEST_POSTGRES to a dedicated PostgreSQL test database.");
        var schema = "test_" + Guid.NewGuid().ToString("N");
        var admin = new NpgsqlConnectionStringBuilder(connectionString);
        await using (var source = NpgsqlDataSource.Create(admin.ConnectionString))
        {
            await using var create = source.CreateCommand($"CREATE SCHEMA {schema}");
            await create.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }
        try
        {
            admin.SearchPath = schema;
            await using var source = NpgsqlDataSource.Create(admin.ConnectionString);
            await using (var migration = source.CreateCommand(await File.ReadAllTextAsync(
                Path.Combine(AppContext.BaseDirectory, "001_financial_transactions.sql"), TestContext.Current.CancellationToken)))
                await migration.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            var repository = new PostgresTransactionRepository(source);
            var value = new FinancialTransaction(Guid.NewGuid(), Guid.NewGuid(), TransactionType.Debit, 999_999_999.99m,
                "teste", DateTimeOffset.Parse("2026-10-22T01:00:00Z"), new DateOnly(2026, 10, 21), DateTimeOffset.UtcNow);
            await repository.InsertAsync(value, TestContext.Current.CancellationToken);
            var actual = await repository.GetAsync(value.Id, TestContext.Current.CancellationToken);
            Assert.NotNull(actual);
            Assert.Equal(value.Amount, actual.Amount);
            Assert.Equal(value.BusinessDate, actual.BusinessDate);
            Assert.Equal([value.Id], (await repository.ListAsync(value.BusinessDate, value.BusinessDate, TransactionType.Debit,
                null, null, 2, TestContext.Current.CancellationToken)).Select(x => x.Id));
        }
        finally
        {
            await using var source = NpgsqlDataSource.Create(connectionString);
            await using var drop = source.CreateCommand($"DROP SCHEMA {schema} CASCADE");
            await drop.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }
    }
}
