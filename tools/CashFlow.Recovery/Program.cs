using Npgsql;
using System.Globalization;

namespace CashFlow.Recovery;

internal static class Program
{
    private static async Task Main()
    {
        var transactionsConnection = Environment.GetEnvironmentVariable("CASHFLOW_RECOVERY_TRANSACTIONS")
            ?? throw new InvalidOperationException("CASHFLOW_RECOVERY_TRANSACTIONS is required (read-only Transactions credentials).");
        var consolidationConnection = Environment.GetEnvironmentVariable("CASHFLOW_RECOVERY_CONSOLIDATION")
            ?? throw new InvalidOperationException("CASHFLOW_RECOVERY_CONSOLIDATION is required.");

        await using var transactions = NpgsqlDataSource.Create(transactionsConnection);
        await using var consolidation = NpgsqlDataSource.Create(consolidationConnection);
        var result = await new ProjectionRebuilder(transactions, consolidation).RunAsync(CancellationToken.None);
        Console.WriteLine($"Rebuild complete: old={result.PreviousGenerationId} new={result.NewGenerationId} " +
            $"transactions={result.TransactionCount} " +
            $"credits={result.TotalCredits.ToString("F2", CultureInfo.InvariantCulture)} " +
            $"debits={result.TotalDebits.ToString("F2", CultureInfo.InvariantCulture)}.");
    }
}
