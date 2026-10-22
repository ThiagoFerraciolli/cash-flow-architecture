using CashFlow.Transactions.Domain;

namespace CashFlow.Transactions.Application;

public interface ITransactionRepository
{
    Task InsertAsync(FinancialTransaction transaction, CancellationToken cancellationToken);
    Task<FinancialTransaction?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<FinancialTransaction>> ListAsync(DateOnly from, DateOnly to, TransactionType? type,
        DateOnly? afterDate, Guid? afterId, int limit, CancellationToken cancellationToken);
}

public sealed class TransactionService(ITransactionRepository repository, TimeProvider clock, TimeZoneInfo businessTimeZone)
{
    public async Task<FinancialTransaction> CreateAsync(TransactionType type, decimal amount, string? description,
        DateTimeOffset occurredAt, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        if (occurredAt > now.AddMinutes(5)) throw new ArgumentOutOfRangeException(nameof(occurredAt));
        var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(occurredAt, businessTimeZone).DateTime);
        var transaction = new FinancialTransaction(Guid.NewGuid(), Guid.NewGuid(), type, amount,
            description, occurredAt, date, now);
        await repository.InsertAsync(transaction, cancellationToken);
        return transaction;
    }

    public Task<FinancialTransaction?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        repository.GetAsync(id, cancellationToken);

    public Task<IReadOnlyList<FinancialTransaction>> ListAsync(DateOnly from, DateOnly to, TransactionType? type,
        DateOnly? afterDate, Guid? afterId, int limit, CancellationToken cancellationToken) =>
        repository.ListAsync(from, to, type, afterDate, afterId, limit, cancellationToken);
}
