namespace CashFlow.Consolidation.Application;

public sealed record DailyConsolidation(DateOnly BusinessDate, decimal TotalCredits, decimal TotalDebits,
    long TransactionCount, DateTimeOffset? LastUpdatedAt);

public sealed record ConsolidationPage(Guid GenerationId, bool Maintenance,
    IReadOnlyList<DailyConsolidation> Days);

public interface IConsolidationQuery
{
    Task<ConsolidationPage> GetAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);
    Task ProbeAsync(CancellationToken cancellationToken);
}
