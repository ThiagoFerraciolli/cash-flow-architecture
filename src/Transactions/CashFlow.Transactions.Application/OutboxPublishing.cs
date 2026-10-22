namespace CashFlow.Transactions.Application;

public sealed record OutboxLease(Guid EventId, Guid TransactionId, Guid LeaseToken,
    string Payload, IReadOnlyDictionary<string, string> Headers, int Attempts);

public enum OutboxFailureKind { Systemic, Isolated, Permanent }

public interface IOutboxLeaseStore
{
    Task<IReadOnlyList<OutboxLease>> ClaimAsync(int limit, CancellationToken cancellationToken);
    Task<bool> RenewAsync(OutboxLease lease, CancellationToken cancellationToken);
    Task<bool> MarkPublishedAsync(OutboxLease lease, CancellationToken cancellationToken);
    Task<bool> MarkFailureAsync(OutboxLease lease, OutboxFailureKind kind, TimeSpan delay,
        string reason, CancellationToken cancellationToken);
}

public interface IOutboxEventPublisher
{
    Task InitializeAsync(CancellationToken cancellationToken);
    Task ProbeAsync(CancellationToken cancellationToken);
    Task PublishAsync(OutboxLease lease, CancellationToken cancellationToken);
}

public sealed class InvalidOutboxEventException(string message) : Exception(message);
public sealed class UnroutableOutboxEventException(string message) : Exception(message);
