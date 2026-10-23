using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CashFlow.Consolidation.Application;
using CashFlow.Consolidation.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using RabbitMQ.Client;
using Xunit;

namespace CashFlow.Consolidation.IntegrationTests;

public sealed class RabbitConsumerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("{", "malformed")]
    [InlineData("{\"schemaVersion\":2}", "version")]
    [InlineData("{\"schemaVersion\":1,\"eventType\":\"Other\"}", "type")]
    public async Task Invalid_contract_is_confirmed_into_dlq_without_projection(string payload, string _)
    {
        await WithRabbit(async (uri, channel, _) =>
        {
            var fake = new FakeProjection();
            await using var running = await StartConsumer(uri, fake);
            await Publish(channel, payload);
            var delivery = await WaitForMessage(channel, RabbitMqTransactionConsumer.ErrorQueue);
            Assert.Equal(payload, Encoding.UTF8.GetString(delivery.Body.ToArray()));
            Assert.Equal(0, fake.Applied);
            Assert.Null(await channel.BasicGetAsync(RabbitMqTransactionConsumer.MainQueue, true, Token));
        });
    }

    [Fact]
    public async Task Invalid_amount_is_confirmed_into_dlq()
    {
        await WithRabbit(async (uri, channel, _) =>
        {
            var fake = new FakeProjection();
            await using var running = await StartConsumer(uri, fake);
            await Publish(channel, Payload(amount: "0.00"));
            Assert.NotNull(await WaitForMessage(channel, RabbitMqTransactionConsumer.ErrorQueue));
            Assert.Equal(0, fake.Applied);
        });
    }

    [Fact]
    public async Task Valid_event_is_acknowledged_after_projection_returns()
    {
        await WithRabbit(async (uri, channel, _) =>
        {
            var fake = new FakeProjection();
            await using var running = await StartConsumer(uri, fake);
            await Publish(channel, Payload());
            await WaitUntil(() => Task.FromResult(fake.Applied == 1));
            Assert.Null(await channel.BasicGetAsync(RabbitMqTransactionConsumer.MainQueue, true, Token));
        });
    }

    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 15)]
    [InlineData(3, 30)]
    [InlineData(4, 60)]
    public async Task Isolated_failure_is_confirmed_into_retry_with_incremented_attempt(
        int attempt, int delaySeconds)
    {
        await WithRabbit(async (uri, channel, _) =>
        {
            var fake = new FakeProjection { Failure = new IOException("isolated") };
            await using var running = await StartConsumer(uri, fake);
            var payload = Payload();
            await Publish(channel, payload, attempt);
            var retry = await WaitForMessage(channel, $"consolidation.retry.{delaySeconds}s");
            Assert.Equal(payload, Encoding.UTF8.GetString(retry.Body.ToArray()));
            Assert.Equal(attempt + 1, retry.BasicProperties.Headers!["retryAttempt"]);
            Assert.Equal("event-test", retry.BasicProperties.MessageId);
            Assert.Equal("correlation-test", retry.BasicProperties.CorrelationId);
            Assert.True(retry.BasicProperties.Headers.ContainsKey("traceparent"));
            Assert.Null(await channel.BasicGetAsync(RabbitMqTransactionConsumer.MainQueue, true, Token));
        });
    }

    [Fact]
    public async Task Fifth_failed_attempt_is_confirmed_into_dlq()
    {
        await WithRabbit(async (uri, channel, _) =>
        {
            var fake = new FakeProjection { Failure = new IOException("isolated") };
            await using var running = await StartConsumer(uri, fake);
            await Publish(channel, Payload(), attempt: 5);
            var dead = await WaitForMessage(channel, RabbitMqTransactionConsumer.ErrorQueue);
            Assert.Equal(5, dead.BasicProperties.Headers!["retryAttempt"]);
            Assert.Null(await channel.BasicGetAsync(RabbitMqTransactionConsumer.MainQueue, true, Token));
        });
    }

    [Fact]
    public async Task Dlq_publication_failure_leaves_original_unacknowledged()
    {
        await WithRabbit(async (uri, channel, _) =>
        {
            await using var running = await StartConsumer(uri, new FakeProjection());
            await channel.QueueUnbindAsync(RabbitMqTransactionConsumer.ErrorQueue, "cashflow.errors",
                "transaction.created.v1.failed", cancellationToken: Token);
            await Publish(channel, "{");
            await WaitUntil(() => Task.FromResult(!running.Consumer.Ready));
            await running.StopAsync();
            var original = await WaitForMessage(channel, RabbitMqTransactionConsumer.MainQueue);
            Assert.True(original.Redelivered);
            Assert.Null(await channel.BasicGetAsync(RabbitMqTransactionConsumer.ErrorQueue, true, Token));
        });
    }

    [Fact]
    public async Task Retry_publication_failure_leaves_original_unacknowledged()
    {
        await WithRabbit(async (uri, channel, _) =>
        {
            await using var running = await StartConsumer(uri,
                new FakeProjection { Failure = new IOException("isolated") });
            await channel.QueueUnbindAsync("consolidation.retry.5s", "cashflow.retry", "retry.5s",
                cancellationToken: Token);
            await Publish(channel, Payload());
            await WaitUntil(() => Task.FromResult(!running.Consumer.Ready));
            await running.StopAsync();
            var original = await WaitForMessage(channel, RabbitMqTransactionConsumer.MainQueue);
            Assert.True(original.Redelivered);
            Assert.Null(await channel.BasicGetAsync("consolidation.retry.5s", true, Token));
        });
    }

    [Fact]
    public async Task Database_outage_pauses_without_retry_then_recovers()
    {
        await WithRabbit(async (uri, channel, _) =>
        {
            var fake = new FakeProjection { Available = false };
            await using var running = await StartConsumer(uri, fake, probeWithoutDatabase: true);
            await Publish(channel, Payload());
            await WaitUntil(() => Task.FromResult(!running.Consumer.Ready));
            await running.StopAsync();
            var original = await channel.BasicGetAsync(RabbitMqTransactionConsumer.MainQueue, false, Token);
            Assert.NotNull(original);
            Assert.True(original.Redelivered);
            Assert.False(original.BasicProperties.Headers?.ContainsKey("retryAttempt") ?? false);
            Assert.Null(await channel.BasicGetAsync("consolidation.retry.5s", true, Token));
            await channel.BasicNackAsync(original.DeliveryTag, false, true, Token);
            fake.Available = true;
            await using var recovered = await StartConsumer(uri, fake);
            await WaitUntil(() => Task.FromResult(fake.Applied == 1));
            Assert.Null(await channel.BasicGetAsync(RabbitMqTransactionConsumer.MainQueue, true, Token));
        });
    }

    [Fact]
    public async Task Commit_then_consumer_crash_before_ack_redelivers_without_second_effect()
    {
        await ProjectionTests.WithDatabase(async source => await WithRabbit(async (uri, channel, _) =>
        {
            var projection = new PostgresTransactionProjection(source);
            var paused = new PauseAfterCommitProjection(projection);
            await using var first = await StartConsumer(uri, paused);
            var eventId = Guid.NewGuid();
            var transactionId = Guid.NewGuid();
            await Publish(channel, Payload(eventId: eventId, transactionId: transactionId));
            await paused.Committed.Task.WaitAsync(TimeSpan.FromSeconds(10), Token);
            Assert.Equal((100m, 0m, 1L), await ProjectionTests.Daily(source, "2026-10-21"));
            await first.StopAsync(); // Connection closes without acknowledging the committed delivery.
            var recovered = new CountingProjection(projection);
            await using var second = await StartConsumer(uri, recovered);
            await WaitUntil(() => Task.FromResult(recovered.Duplicates == 1));
            Assert.Equal(1L, await ProjectionTests.Count(source, "processed_transactions"));
            Assert.Equal((100m, 0m, 1L), await ProjectionTests.Daily(source, "2026-10-21"));
            Assert.Null(await channel.BasicGetAsync(RabbitMqTransactionConsumer.MainQueue, true, Token));
        }));
    }

    [Fact]
    public async Task New_event_for_same_financial_fact_is_acknowledged_without_second_effect()
    {
        await ProjectionTests.WithDatabase(async source => await WithRabbit(async (uri, channel, _) =>
        {
            var projection = new CountingProjection(new PostgresTransactionProjection(source));
            await using var running = await StartConsumer(uri, projection);
            var transactionId = Guid.NewGuid();
            await Publish(channel, Payload(eventId: Guid.NewGuid(), transactionId: transactionId));
            await WaitUntil(async () => await ProjectionTests.Count(source, "processed_transactions") == 1);
            await Publish(channel, Payload(eventId: Guid.NewGuid(), transactionId: transactionId));
            await WaitUntil(() => Task.FromResult(projection.Duplicates == 1));
            Assert.Equal(1L, await ProjectionTests.Count(source, "processed_transactions"));
            Assert.Equal((100m, 0m, 1L), await ProjectionTests.Daily(source, "2026-10-21"));
        }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Conflicting_financial_or_event_identity_is_confirmed_into_dlq(bool reuseEvent)
    {
        await ProjectionTests.WithDatabase(async source => await WithRabbit(async (uri, channel, _) =>
        {
            await using var running = await StartConsumer(uri, new PostgresTransactionProjection(source));
            var eventId = Guid.NewGuid();
            var transactionId = Guid.NewGuid();
            await Publish(channel, Payload(eventId: eventId, transactionId: transactionId));
            await WaitUntil(async () => await ProjectionTests.Count(source, "processed_transactions") == 1);
            await Publish(channel, Payload(amount: reuseEvent ? "100.00" : "101.00",
                eventId: reuseEvent ? eventId : Guid.NewGuid(),
                transactionId: reuseEvent ? Guid.NewGuid() : transactionId));
            Assert.NotNull(await WaitForMessage(channel, RabbitMqTransactionConsumer.ErrorQueue));
            Assert.Equal(1L, await ProjectionTests.Count(source, "processed_transactions"));
            Assert.Equal((100m, 0m, 1L), await ProjectionTests.Daily(source, "2026-10-21"));
        }));
    }

    private static string Payload(string amount = "100.00", Guid? eventId = null,
        Guid? transactionId = null) => JsonSerializer.Serialize(new
    {
        schemaVersion = 1,
        eventId = eventId ?? Guid.NewGuid(),
        eventType = "TransactionCreated",
        eventOccurredAt = "2026-10-22T12:00:00Z",
        transactionId = transactionId ?? Guid.NewGuid(),
        transactionType = "CREDIT",
        amount,
        businessDate = "2026-10-21"
    });

    private static async Task Publish(IChannel channel, string payload, int? attempt = null)
    {
        var headers = new Dictionary<string, object?>
        {
            ["traceparent"] = Encoding.UTF8.GetBytes("00-0123456789abcdef0123456789abcdef-0123456789abcdef-01")
        };
        if (attempt.HasValue) headers["retryAttempt"] = attempt.Value;
        await channel.BasicPublishAsync("cashflow.events", "transaction.created.v1", mandatory: true,
            basicProperties: new BasicProperties
            {
                DeliveryMode = DeliveryModes.Persistent,
                MessageId = "event-test",
                CorrelationId = "correlation-test",
                ContentType = "application/json",
                Headers = headers
            }, body: Encoding.UTF8.GetBytes(payload), cancellationToken: Token);
    }

    private static async Task<BasicGetResult> WaitForMessage(IChannel channel, string queue)
    {
        BasicGetResult? delivery = null;
        await WaitUntil(async () => (delivery = await channel.BasicGetAsync(queue, true, Token)) is not null);
        return delivery!;
    }

    private static async Task WaitUntil(Func<Task<bool>> condition)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(10);
        while (!await condition())
        {
            Assert.True(DateTimeOffset.UtcNow < deadline, "Timed out waiting for RabbitMQ state.");
            await Task.Delay(50, Token);
        }
    }

    private static async Task<RunningConsumer> StartConsumer(string uri, ITransactionProjection projection,
        bool probeWithoutDatabase = false)
    {
        if (projection is FakeProjection fake) fake.IgnoreProbe = probeWithoutDatabase;
        var consumer = new RabbitMqTransactionConsumer(uri, projection,
            NullLogger<RabbitMqTransactionConsumer>.Instance);
        var cancellation = new CancellationTokenSource();
        var task = consumer.RunAsync(cancellation.Token);
        await WaitUntil(() => Task.FromResult(consumer.Ready));
        return new RunningConsumer(consumer, cancellation, task);
    }

    private sealed class RunningConsumer(RabbitMqTransactionConsumer consumer,
        CancellationTokenSource cancellation, Task task) : IAsyncDisposable
    {
        public RabbitMqTransactionConsumer Consumer => consumer;
        public async Task StopAsync()
        {
            if (!cancellation.IsCancellationRequested) await cancellation.CancelAsync();
            try { await task; }
            catch (OperationCanceledException) { }
        }
        public async ValueTask DisposeAsync()
        {
            await StopAsync();
            cancellation.Dispose();
        }
    }

    private sealed class FakeProjection : ITransactionProjection
    {
        public bool Available { get; set; } = true;
        public bool IgnoreProbe { get; set; }
        public Exception? Failure { get; set; }
        private int _applied;
        public int Applied => Volatile.Read(ref _applied);
        public Task<ApplyTransactionResult> ApplyAsync(TransactionCreatedFact fact, CancellationToken cancellationToken)
        {
            if (!Available) throw new NpgsqlException("simulated database outage");
            if (Failure is not null) throw Failure;
            Interlocked.Increment(ref _applied);
            return Task.FromResult(ApplyTransactionResult.Applied);
        }
        public Task ProbeAsync(CancellationToken cancellationToken)
        {
            if (!IgnoreProbe && !Available) throw new NpgsqlException("simulated database outage");
            return Task.CompletedTask;
        }
    }

    private sealed class PauseAfterCommitProjection(ITransactionProjection inner) : ITransactionProjection
    {
        public TaskCompletionSource Committed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<ApplyTransactionResult> ApplyAsync(TransactionCreatedFact fact,
            CancellationToken cancellationToken)
        {
            var result = await inner.ApplyAsync(fact, cancellationToken);
            Committed.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return result;
        }
        public Task ProbeAsync(CancellationToken cancellationToken) => inner.ProbeAsync(cancellationToken);
    }

    private sealed class CountingProjection(ITransactionProjection inner) : ITransactionProjection
    {
        private int _duplicates;
        public int Duplicates => Volatile.Read(ref _duplicates);
        public async Task<ApplyTransactionResult> ApplyAsync(TransactionCreatedFact fact,
            CancellationToken cancellationToken)
        {
            var result = await inner.ApplyAsync(fact, cancellationToken);
            if (result == ApplyTransactionResult.Duplicate) Interlocked.Increment(ref _duplicates);
            return result;
        }
        public Task ProbeAsync(CancellationToken cancellationToken) => inner.ProbeAsync(cancellationToken);
    }

    private static async Task WithRabbit(Func<string, IChannel, string, Task> test)
    {
        var configured = Environment.GetEnvironmentVariable("CASHFLOW_TEST_RABBITMQ");
        Assert.False(string.IsNullOrWhiteSpace(configured), "Set CASHFLOW_TEST_RABBITMQ.");
        var baseUri = new Uri(configured);
        var vhost = "test_" + Guid.NewGuid().ToString("N");
        var management = new UriBuilder("http", baseUri.Host, 15672).Uri;
        using var http = new HttpClient { BaseAddress = management };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes(Uri.UnescapeDataString(baseUri.UserInfo))));
        var encoded = Uri.EscapeDataString(vhost);
        using (var create = await http.PutAsync($"api/vhosts/{encoded}", null, Token))
            create.EnsureSuccessStatusCode();
        using (var permissions = await http.PutAsync($"api/permissions/{encoded}/cashflow",
            new StringContent("{\"configure\":\".*\",\"write\":\".*\",\"read\":\".*\"}",
                Encoding.UTF8, "application/json"), Token))
            permissions.EnsureSuccessStatusCode();
        try
        {
            var uri = new UriBuilder(baseUri) { Path = vhost }.Uri.ToString();
            var factory = new ConnectionFactory { Uri = new Uri(uri) };
            await using var connection = await factory.CreateConnectionAsync(Token);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: Token);
            await test(uri, channel, vhost);
        }
        finally
        {
            using var delete = await http.DeleteAsync($"api/vhosts/{encoded}", Token);
            delete.EnsureSuccessStatusCode();
        }
    }
}
