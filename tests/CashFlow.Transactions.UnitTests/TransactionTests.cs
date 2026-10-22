using CashFlow.Transactions.Application;
using CashFlow.Transactions.Domain;
using Xunit;

namespace CashFlow.Transactions.UnitTests;

public sealed class TransactionTests
{
    [Theory]
    [InlineData(TransactionType.Credit, "0.01")]
    [InlineData(TransactionType.Debit, "999999999.99")]
    public void Accepts_valid_amount_and_type(TransactionType type, string text)
    {
        var amount = decimal.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
        var value = Make(type, amount, "  texto  ");
        Assert.Equal(type, value.Type);
        Assert.Equal(amount, value.Amount);
        Assert.Equal("  texto  ", value.Description);
    }

    [Theory]
    [InlineData("0.00")]
    [InlineData("1000000000.00")]
    [InlineData("1.001")]
    public void Rejects_invalid_amount(string text)
    {
        var amount = decimal.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Throws<ArgumentOutOfRangeException>(() => Make(TransactionType.Credit, amount));
    }

    [Fact]
    public void Rejects_invalid_type() => Assert.Throws<ArgumentOutOfRangeException>(() => Make((TransactionType)99, 1m));

    [Fact]
    public void Normalizes_only_empty_description()
    {
        Assert.Null(Make(TransactionType.Debit, 1m, "").Description);
        Assert.Equal(" ", Make(TransactionType.Debit, 1m, " ").Description);
        Assert.Throws<ArgumentOutOfRangeException>(() => Make(TransactionType.Debit, 1m, new string('x', 501)));
    }

    [Theory]
    [InlineData("2026-10-22T01:30:00Z", "2026-10-21")]
    [InlineData("2026-10-22T00:30:00-03:00", "2026-10-22")]
    [InlineData("2026-10-22T23:30:00+02:00", "2026-10-22")]
    public async Task Calculates_business_date_from_instant_in_sao_paulo(string occurredText, string dateText)
    {
        var repository = new FakeRepository();
        var service = new TransactionService(repository, new FixedClock(DateTimeOffset.Parse("2026-10-23T00:00:00Z")),
            TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"));
        var value = await service.CreateAsync(TransactionType.Credit, 1.23m, null, DateTimeOffset.Parse(occurredText), TestContext.Current.CancellationToken);
        Assert.Equal(DateOnly.Parse(dateText), value.BusinessDate);
        Assert.Equal(DateTimeOffset.Parse(occurredText).ToUniversalTime(), value.OccurredAt);
        Assert.Same(value, repository.Values.Single());
    }

    [Fact]
    public async Task Rejects_future_beyond_five_minutes()
    {
        var repository = new FakeRepository();
        var service = new TransactionService(repository, new FixedClock(DateTimeOffset.Parse("2026-10-22T00:00:00Z")), TimeZoneInfo.Utc);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.CreateAsync(TransactionType.Debit, 1m,
            null, DateTimeOffset.Parse("2026-10-22T00:05:01Z"), TestContext.Current.CancellationToken));
        Assert.Empty(repository.Values);
    }

    [Fact]
    public async Task Queries_by_id_date_type_and_cursor()
    {
        var repository = new FakeRepository();
        var service = new TransactionService(repository, new FixedClock(DateTimeOffset.Parse("2026-10-23T00:00:00Z")),
            TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"));
        var token = TestContext.Current.CancellationToken;
        var first = await service.CreateAsync(TransactionType.Credit, 1m, null, DateTimeOffset.Parse("2026-10-21T15:00:00Z"), token);
        var second = await service.CreateAsync(TransactionType.Debit, 2m, null, DateTimeOffset.Parse("2026-10-22T15:00:00Z"), token);
        Assert.Same(first, await service.GetAsync(first.Id, token));
        Assert.Null(await service.GetAsync(Guid.NewGuid(), token));
        var from = new DateOnly(2026, 10, 21);
        var to = new DateOnly(2026, 10, 22);
        Assert.Equal(2, (await service.ListAsync(from, to, null, null, null, 10, token)).Count);
        Assert.Equal([second], await service.ListAsync(from, to, TransactionType.Debit, null, null, 10, token));
        Assert.Equal([first], await service.ListAsync(from, to, null, null, null, 1, token));
        Assert.Equal([second], await service.ListAsync(from, to, null, first.BusinessDate, first.Id, 1, token));
    }

    private static FinancialTransaction Make(TransactionType type, decimal amount, string? description = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), type, amount, description, DateTimeOffset.Parse("2026-10-22T00:00:00Z"),
            new DateOnly(2026, 10, 21), DateTimeOffset.Parse("2026-10-22T00:00:01Z"));

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeRepository : ITransactionRepository
    {
        public List<FinancialTransaction> Values { get; } = [];
        public Task InsertAsync(FinancialTransaction value, CancellationToken ct) { Values.Add(value); return Task.CompletedTask; }
        public Task<FinancialTransaction?> GetAsync(Guid id, CancellationToken ct) => Task.FromResult(Values.SingleOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<FinancialTransaction>> ListAsync(DateOnly from, DateOnly to, TransactionType? type,
            DateOnly? afterDate, Guid? afterId, int limit, CancellationToken ct) => Task.FromResult<IReadOnlyList<FinancialTransaction>>(
                Values.Where(x => x.BusinessDate >= from && x.BusinessDate <= to && (type is null || x.Type == type) &&
                    (afterDate is null || x.BusinessDate > afterDate || x.BusinessDate == afterDate && x.Id.CompareTo(afterId!.Value) > 0))
                    .OrderBy(x => x.BusinessDate).ThenBy(x => x.Id).Take(limit).ToArray());
    }
}
