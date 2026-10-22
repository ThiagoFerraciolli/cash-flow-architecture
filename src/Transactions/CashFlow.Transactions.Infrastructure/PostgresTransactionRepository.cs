using CashFlow.Transactions.Application;
using CashFlow.Transactions.Domain;
using Npgsql;
using NpgsqlTypes;

namespace CashFlow.Transactions.Infrastructure;

public sealed class PostgresTransactionRepository(NpgsqlDataSource dataSource) : ITransactionRepository
{
    public async Task<FinancialTransaction?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT id, creation_event_id, type, amount, description, occurred_at, business_date, created_at
            FROM financial_transactions WHERE id = $1
            """);
        command.Parameters.AddWithValue(id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
    }

    public async Task<IReadOnlyList<FinancialTransaction>> ListAsync(DateOnly from, DateOnly to,
        TransactionType? type, DateOnly? afterDate, Guid? afterId, int limit, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT id, creation_event_id, type, amount, description, occurred_at, business_date, created_at
            FROM financial_transactions
            WHERE business_date >= $1 AND business_date <= $2
              AND ($3::text IS NULL OR type = $3)
              AND ($4::date IS NULL OR (business_date, id) > ($4, $5))
            ORDER BY business_date, id LIMIT $6
            """);
        command.Parameters.AddWithValue(from);
        command.Parameters.AddWithValue(to);
        command.Parameters.AddWithValue(NpgsqlDbType.Text, type is null ? DBNull.Value : type.Value.ToString().ToUpperInvariant());
        command.Parameters.AddWithValue(NpgsqlDbType.Date, (object?)afterDate ?? DBNull.Value);
        command.Parameters.AddWithValue(NpgsqlDbType.Uuid, (object?)afterId ?? DBNull.Value);
        command.Parameters.AddWithValue(limit);
        var values = new List<FinancialTransaction>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) values.Add(Read(reader));
        return values;
    }

    internal static FinancialTransaction Read(NpgsqlDataReader reader) => new(
        reader.GetGuid(0), reader.GetGuid(1), Enum.Parse<TransactionType>(reader.GetString(2), true),
        reader.GetDecimal(3), reader.IsDBNull(4) ? null : reader.GetString(4),
        reader.GetFieldValue<DateTimeOffset>(5), reader.GetFieldValue<DateOnly>(6), reader.GetFieldValue<DateTimeOffset>(7));
}
