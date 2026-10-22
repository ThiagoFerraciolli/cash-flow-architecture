namespace CashFlow.Transactions.Domain;

public enum TransactionType { Credit, Debit }

public sealed class FinancialTransaction
{
    public Guid Id { get; }
    public Guid CreationEventId { get; }
    public TransactionType Type { get; }
    public decimal Amount { get; }
    public string? Description { get; }
    public DateTimeOffset OccurredAt { get; }
    public DateOnly BusinessDate { get; }
    public DateTimeOffset CreatedAt { get; }

    public FinancialTransaction(Guid id, Guid creationEventId, TransactionType type, decimal amount,
        string? description, DateTimeOffset occurredAt, DateOnly businessDate, DateTimeOffset createdAt)
    {
        if (id == Guid.Empty || creationEventId == Guid.Empty) throw new ArgumentException("Identifiers must be nonempty.");
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
        if (amount < 0.01m || amount > 999_999_999.99m || decimal.Round(amount, 2) != amount)
            throw new ArgumentOutOfRangeException(nameof(amount));
        if (description?.Length > 500) throw new ArgumentOutOfRangeException(nameof(description));
        Id = id;
        CreationEventId = creationEventId;
        Type = type;
        Amount = amount;
        Description = description == string.Empty ? null : description;
        OccurredAt = occurredAt.ToUniversalTime();
        BusinessDate = businessDate;
        CreatedAt = createdAt.ToUniversalTime();
    }
}
