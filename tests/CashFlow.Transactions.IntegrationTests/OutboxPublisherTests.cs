using System.Globalization;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using CashFlow.Transactions.Application;
using CashFlow.Transactions.Domain;
using CashFlow.Transactions.Infrastructure;
using CashFlow.Transactions.Worker;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using RabbitMQ.Client;
using Xunit;

namespace CashFlow.Transactions.IntegrationTests;

public sealed class OutboxPublisherTests
{
    [Fact]
    public async Task Claim_is_concurrent_and_expired_lease_recovers_without_stale_updates()
    {
        await WithDatabase(async source =>
        {
            var first = await Seed(source);
            var second = await Seed(source);
            var storeA = new PostgresOutboxLeaseStore(source);
            var storeB = new PostgresOutboxLeaseStore(source);
            var claims = await Task.WhenAll(storeA.ClaimAsync(1, Token), storeB.ClaimAsync(1, Token));
            Assert.Single(claims[0]);
            Assert.Single(claims[1]);
            Assert.NotEqual(claims[0][0].EventId, claims[1][0].EventId);
            Assert.Contains(first.EventId, claims.SelectMany(x => x).Select(x => x.EventId));
            Assert.Contains(second.EventId, claims.SelectMany(x => x).Select(x => x.EventId));
            Assert.Empty(await storeB.ClaimAsync(20, Token));
            Assert.True(await storeA.RenewAsync(claims[1][0], Token));

            var oldLease = claims[0][0];
            await using (var expire = source.CreateCommand("UPDATE outbox_messages SET lease_until = clock_timestamp() - interval '1 second' WHERE event_id = $1"))
            {
                expire.Parameters.AddWithValue(oldLease.EventId);
                await expire.ExecuteNonQueryAsync(Token);
            }
            var newLease = Assert.Single(await storeB.ClaimAsync(20, Token));
            Assert.Equal(oldLease.EventId, newLease.EventId);
            Assert.NotEqual(oldLease.LeaseToken, newLease.LeaseToken);
            Assert.False(await storeA.MarkPublishedAsync(oldLease, Token));
            Assert.False(await storeA.RenewAsync(oldLease, Token));
            Assert.False(await storeA.MarkFailureAsync(oldLease, OutboxFailureKind.Permanent,
                TimeSpan.Zero, "stale", Token));
            Assert.True(await storeB.MarkPublishedAsync(newLease, Token));
            Assert.Equal("PUBLISHED", await State(source, newLease.EventId));
        });
    }

    [Fact]
    public async Task Systemic_failure_preserves_attempts_and_isolated_failure_suspends_after_twenty()
    {
        await WithDatabase(async source =>
        {
            var fact = await Seed(source);
            var store = new PostgresOutboxLeaseStore(source);
            var lease = Assert.Single(await store.ClaimAsync(1, Token));
            Assert.True(await store.MarkFailureAsync(lease, OutboxFailureKind.Systemic,
                TimeSpan.Zero, "broker-unavailable", Token));
            Assert.Equal(0, await Attempts(source, fact.EventId));
            for (var attempt = 0; attempt < 20; attempt++)
            {
                lease = Assert.Single(await store.ClaimAsync(1, Token));
                Assert.True(await store.MarkFailureAsync(lease, OutboxFailureKind.Isolated,
                    TimeSpan.Zero, "isolated-publish-failure", Token));
            }
            Assert.Equal(20, await Attempts(source, fact.EventId));
            Assert.Equal("SUSPENDED", await State(source, fact.EventId));
            Assert.Empty(await store.ClaimAsync(1, Token));
        });
    }

    [Fact]
    public async Task Confirmed_persistent_event_is_published_with_normative_payload()
    {
        await WithDatabase(async source => await WithQueue(async (uri, exchange, key, queue, channel) =>
        {
            using var activity = new Activity("outbox-test").SetIdFormat(ActivityIdFormat.W3C);
            activity.TraceStateString = "vendor=value";
            activity.Start();
            var fact = await Seed(source);
            activity.Stop();
            await using var publisher = new RabbitMqOutboxPublisher(uri, exchange, key);
            var worker = new OutboxWorker(new PostgresOutboxLeaseStore(source), publisher,
                NullLogger<OutboxWorker>.Instance);
            await worker.RunBatchAsync(Token);
            Assert.Equal("PUBLISHED", await State(source, fact.EventId));
            await using var inspect = source.CreateCommand("SELECT published_at IS NOT NULL, lease_token IS NULL FROM outbox_messages WHERE event_id = $1");
            inspect.Parameters.AddWithValue(fact.EventId);
            await using var reader = await inspect.ExecuteReaderAsync(Token);
            Assert.True(await reader.ReadAsync(Token));
            Assert.True(reader.GetBoolean(0));
            Assert.True(reader.GetBoolean(1));
            var delivery = await channel.BasicGetAsync(queue, autoAck: true, cancellationToken: Token);
            Assert.NotNull(delivery);
            Assert.Equal(fact.EventId.ToString(), delivery.BasicProperties.MessageId);
            Assert.Equal(DeliveryModes.Persistent, delivery.BasicProperties.DeliveryMode);
            using var payload = JsonDocument.Parse(delivery.Body);
            Assert.Equal(fact.EventId, payload.RootElement.GetProperty("eventId").GetGuid());
            Assert.Equal(fact.TransactionId, payload.RootElement.GetProperty("transactionId").GetGuid());
            Assert.Equal("TransactionCreated", payload.RootElement.GetProperty("eventType").GetString());
            Assert.Equal(1, payload.RootElement.GetProperty("schemaVersion").GetInt32());
            Assert.Equal("100.00", payload.RootElement.GetProperty("amount").GetString());
            Assert.Equal("2026-10-21", payload.RootElement.GetProperty("businessDate").GetString());
            Assert.Equal(activity.Id, Encoding.UTF8.GetString((byte[])delivery.BasicProperties.Headers!["traceparent"]!));
            Assert.Equal("vendor=value", Encoding.UTF8.GetString((byte[])delivery.BasicProperties.Headers!["tracestate"]!));
            await using var headers = source.CreateCommand("SELECT headers::text FROM outbox_messages WHERE event_id = $1");
            headers.Parameters.AddWithValue(fact.EventId);
            using var storedHeaders = JsonDocument.Parse((string)(await headers.ExecuteScalarAsync(Token))!);
            Assert.Equal(activity.Id, storedHeaders.RootElement.GetProperty("traceparent").GetString());
            Assert.Equal("vendor=value", storedHeaders.RootElement.GetProperty("tracestate").GetString());
        }));
    }

    [Fact]
    public async Task Mandatory_unroutable_event_stays_pending_and_does_not_consume_attempt()
    {
        await WithDatabase(async source => await WithQueue(async (uri, exchange, _, queue, channel) =>
        {
            var fact = await Seed(source);
            await using var publisher = new RabbitMqOutboxPublisher(uri, exchange, "missing-route");
            var worker = new OutboxWorker(new PostgresOutboxLeaseStore(source), publisher,
                NullLogger<OutboxWorker>.Instance);
            await worker.RunBatchAsync(Token);
            Assert.Equal("PENDING", await State(source, fact.EventId));
            Assert.Equal(0, await Attempts(source, fact.EventId));
            await using var reason = source.CreateCommand("SELECT last_error FROM outbox_messages WHERE event_id = $1");
            reason.Parameters.AddWithValue(fact.EventId);
            Assert.Equal("unroutable", await reason.ExecuteScalarAsync(Token));
            Assert.Null(await channel.BasicGetAsync(queue, autoAck: true, cancellationToken: Token));
        }));
    }

    [Fact]
    public async Task Confirm_before_mark_can_republish_same_event_after_lease_expiry()
    {
        await WithDatabase(async source => await WithQueue(async (uri, exchange, key, queue, channel) =>
        {
            var fact = await Seed(source);
            var store = new PostgresOutboxLeaseStore(source);
            await using var publisher = new RabbitMqOutboxPublisher(uri, exchange, key);
            var firstLease = Assert.Single(await store.ClaimAsync(1, Token));
            await publisher.PublishAsync(firstLease, Token); // Simulates a crash before MarkPublished.
            await using (var expire = source.CreateCommand("UPDATE outbox_messages SET lease_until = clock_timestamp() - interval '1 second' WHERE event_id = $1"))
            {
                expire.Parameters.AddWithValue(fact.EventId);
                await expire.ExecuteNonQueryAsync(Token);
            }
            var worker = new OutboxWorker(store, publisher, NullLogger<OutboxWorker>.Instance);
            await worker.RunBatchAsync(Token);
            Assert.False(await store.MarkPublishedAsync(firstLease, Token));
            Assert.Equal("PUBLISHED", await State(source, fact.EventId));
            var first = await channel.BasicGetAsync(queue, autoAck: true, cancellationToken: Token);
            var second = await channel.BasicGetAsync(queue, autoAck: true, cancellationToken: Token);
            Assert.NotNull(first);
            Assert.NotNull(second);
            using var firstPayload = JsonDocument.Parse(first.Body);
            using var secondPayload = JsonDocument.Parse(second.Body);
            Assert.Equal(fact.EventId, firstPayload.RootElement.GetProperty("eventId").GetGuid());
            Assert.Equal(fact.EventId, secondPayload.RootElement.GetProperty("eventId").GetGuid());
        }));
    }

    [Fact]
    public async Task Broker_failure_then_recovery_publishes_pending_without_suspending()
    {
        await WithDatabase(async source =>
        {
            var fact = await Seed(source);
            var fake = new RecoveringPublisher();
            var worker = new OutboxWorker(new PostgresOutboxLeaseStore(source), fake,
                NullLogger<OutboxWorker>.Instance);
            await worker.RunBatchAsync(Token);
            Assert.Equal("PENDING", await State(source, fact.EventId));
            Assert.Equal(0, await Attempts(source, fact.EventId));
            fake.Available = true;
            await Task.Delay(TimeSpan.FromSeconds(2), Token);
            await worker.RunBatchAsync(Token);
            Assert.Equal("PUBLISHED", await State(source, fact.EventId));
            Assert.Equal(fact.EventId, Assert.Single(fake.Published));
        });
    }

    [Fact]
    public async Task Publish_timeout_keeps_item_pending_without_marking_success()
    {
        await WithDatabase(async source =>
        {
            var fact = await Seed(source);
            var worker = new OutboxWorker(new PostgresOutboxLeaseStore(source), new TimeoutPublisher(),
                NullLogger<OutboxWorker>.Instance);
            await worker.RunBatchAsync(Token);
            Assert.Equal("PENDING", await State(source, fact.EventId));
            Assert.Equal(0, await Attempts(source, fact.EventId));
        });
    }

    [Fact]
    public async Task Invalid_contract_is_suspended_without_losing_payload()
    {
        await WithDatabase(async source =>
        {
            var fact = await Seed(source);
            await using (var corrupt = source.CreateCommand("UPDATE outbox_messages SET payload = '{\"schemaVersion\":99}'::jsonb WHERE event_id = $1"))
            {
                corrupt.Parameters.AddWithValue(fact.EventId);
                await corrupt.ExecuteNonQueryAsync(Token);
            }
            var uri = Environment.GetEnvironmentVariable("CASHFLOW_TEST_RABBITMQ");
            if (string.IsNullOrWhiteSpace(uri)) Assert.Skip("Set CASHFLOW_TEST_RABBITMQ for RabbitMQ tests.");
            await using var publisher = new RabbitMqOutboxPublisher(uri);
            var worker = new OutboxWorker(new PostgresOutboxLeaseStore(source), publisher,
                NullLogger<OutboxWorker>.Instance);
            await worker.RunBatchAsync(Token);
            Assert.Equal("SUSPENDED", await State(source, fact.EventId));
            await using var inspect = source.CreateCommand("SELECT payload::text FROM outbox_messages WHERE event_id = $1");
            inspect.Parameters.AddWithValue(fact.EventId);
            Assert.Contains("99", (string)(await inspect.ExecuteScalarAsync(Token))!);
        });
    }

    private sealed class TimeoutPublisher : IOutboxEventPublisher
    {
        public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ProbeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task PublishAsync(OutboxLease lease, CancellationToken cancellationToken) =>
            throw new TimeoutException("simulated publisher timeout");
    }

    private sealed class RecoveringPublisher : IOutboxEventPublisher
    {
        public bool Available { get; set; }
        public List<Guid> Published { get; } = [];
        public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ProbeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task PublishAsync(OutboxLease lease, CancellationToken cancellationToken)
        {
            if (!Available) throw new System.IO.IOException("simulated broker outage");
            Published.Add(lease.EventId);
            return Task.CompletedTask;
        }
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<(Guid EventId, Guid TransactionId)> Seed(NpgsqlDataSource source)
    {
        var fact = new FinancialTransaction(Guid.NewGuid(), Guid.NewGuid(), TransactionType.Credit, 100m,
            null, DateTimeOffset.Parse("2026-10-21T15:00:00Z", CultureInfo.InvariantCulture),
            new DateOnly(2026, 10, 21), DateTimeOffset.UtcNow);
        var store = new PostgresTransactionCreationStore(source);
        var created = await store.CreateOrReplayAsync(new AuthenticatedClientIdentity("https://test-issuer.example", "client-a"),
            Guid.NewGuid(), CanonicalTransactionHash.Compute(fact), CanonicalTransactionHash.Version,
            fact, Guid.NewGuid().ToString(), Token);
        Assert.Equal(CreateTransactionStatus.Created, created.Status);
        return (fact.CreationEventId, fact.Id);
    }

    private static async Task<string> State(NpgsqlDataSource source, Guid id)
    {
        await using var command = source.CreateCommand("SELECT state FROM outbox_messages WHERE event_id = $1");
        command.Parameters.AddWithValue(id);
        return (string)(await command.ExecuteScalarAsync(Token))!;
    }

    private static async Task<int> Attempts(NpgsqlDataSource source, Guid id)
    {
        await using var command = source.CreateCommand("SELECT attempts FROM outbox_messages WHERE event_id = $1");
        command.Parameters.AddWithValue(id);
        return (int)(await command.ExecuteScalarAsync(Token))!;
    }

    private static async Task WithDatabase(Func<NpgsqlDataSource, Task> test)
    {
        var connection = Environment.GetEnvironmentVariable("CASHFLOW_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connection)) Assert.Skip("Set CASHFLOW_TEST_POSTGRES for PostgreSQL tests.");
        var schema = "test_" + Guid.NewGuid().ToString("N");
        await using (var admin = NpgsqlDataSource.Create(connection))
        {
            await using var create = admin.CreateCommand($"CREATE SCHEMA {schema}");
            await create.ExecuteNonQueryAsync(Token);
        }
        try
        {
            var scoped = new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema }.ConnectionString;
            await using var source = NpgsqlDataSource.Create(scoped);
            foreach (var migration in new[] { "001_financial_transactions.sql", "002_idempotency_outbox.sql" })
            {
                await using var command = source.CreateCommand(await File.ReadAllTextAsync(
                    Path.Combine(AppContext.BaseDirectory, migration), Token));
                await command.ExecuteNonQueryAsync(Token);
            }
            await test(source);
        }
        finally
        {
            await using var admin = NpgsqlDataSource.Create(connection);
            await using var drop = admin.CreateCommand($"DROP SCHEMA {schema} CASCADE");
            await drop.ExecuteNonQueryAsync(Token);
        }
    }

    private static async Task WithQueue(Func<string, string, string, string, IChannel, Task> test)
    {
        var uri = Environment.GetEnvironmentVariable("CASHFLOW_TEST_RABBITMQ");
        if (string.IsNullOrWhiteSpace(uri)) Assert.Skip("Set CASHFLOW_TEST_RABBITMQ for RabbitMQ tests.");
        var suffix = Guid.NewGuid().ToString("N");
        var exchange = "cashflow.test." + suffix;
        var key = "transaction.created.v1";
        var queue = "cashflow.test." + suffix;
        var factory = new ConnectionFactory { Uri = new Uri(uri) };
        await using var connection = await factory.CreateConnectionAsync(Token);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: Token);
        await channel.ExchangeDeclareAsync(exchange, ExchangeType.Direct, durable: true, cancellationToken: Token);
        await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-queue-type"] = "quorum" }, cancellationToken: Token);
        await channel.QueueBindAsync(queue, exchange, key, cancellationToken: Token);
        try { await test(uri, exchange, key, queue, channel); }
        finally
        {
            await channel.QueueDeleteAsync(queue, cancellationToken: Token);
            await channel.ExchangeDeleteAsync(exchange, cancellationToken: Token);
        }
    }
}
