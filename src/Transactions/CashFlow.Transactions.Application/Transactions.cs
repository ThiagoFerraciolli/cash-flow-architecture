using CashFlow.Transactions.Domain;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CashFlow.Transactions.Application;

public interface ITransactionRepository
{
    Task<FinancialTransaction?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<FinancialTransaction>> ListAsync(DateOnly from, DateOnly to, TransactionType? type,
        DateOnly? afterDate, Guid? afterId, int limit, CancellationToken cancellationToken);
}

public sealed record AuthenticatedClientIdentity
{
    public string Issuer { get; }
    public string ClientId { get; }

    public AuthenticatedClientIdentity(string issuer, string clientId)
    {
        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(clientId))
            throw new ArgumentException("A validated issuer and client ID are required.");
        Issuer = issuer;
        ClientId = clientId;
    }
}

public enum CreateTransactionStatus { Created, Replayed, Conflict }
public sealed record CreateTransactionResult(CreateTransactionStatus Status, FinancialTransaction? Transaction);

public interface ITransactionCreationStore
{
    Task<CreateTransactionResult> CreateOrReplayAsync(AuthenticatedClientIdentity identity, Guid key,
        string requestHash, int canonicalVersion, FinancialTransaction candidate, string correlationId,
        CancellationToken cancellationToken);
}

public static class CanonicalTransactionHash
{
    public const int Version = 1;

    public static string Compute(FinancialTransaction transaction)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            type = transaction.Type.ToString().ToUpperInvariant(),
            amount = transaction.Amount.ToString("F2", CultureInfo.InvariantCulture),
            occurredAt = transaction.OccurredAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            description = transaction.Description
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}

public sealed class TransactionService(ITransactionRepository repository, ITransactionCreationStore creationStore,
    TimeProvider clock, TimeZoneInfo businessTimeZone)
{
    public Task<CreateTransactionResult> CreateAsync(AuthenticatedClientIdentity identity, Guid key,
        TransactionType type, decimal amount, string? description, DateTimeOffset occurredAt, string correlationId,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        if (occurredAt > now.AddMinutes(5)) throw new ArgumentOutOfRangeException(nameof(occurredAt));
        var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(occurredAt, businessTimeZone).DateTime);
        var transaction = new FinancialTransaction(Guid.NewGuid(), Guid.NewGuid(), type, amount,
            description, occurredAt, date, now);
        return creationStore.CreateOrReplayAsync(identity, key, CanonicalTransactionHash.Compute(transaction),
            CanonicalTransactionHash.Version, transaction, correlationId, cancellationToken);
    }

    public Task<FinancialTransaction?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        repository.GetAsync(id, cancellationToken);

    public Task<IReadOnlyList<FinancialTransaction>> ListAsync(DateOnly from, DateOnly to, TransactionType? type,
        DateOnly? afterDate, Guid? afterId, int limit, CancellationToken cancellationToken) =>
        repository.ListAsync(from, to, type, afterDate, afterId, limit, cancellationToken);
}
