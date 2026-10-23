using System.Text;
using System.Net.Http.Headers;
using System.Text.Json;
using CashFlow.Consolidation.Application;
using Microsoft.Extensions.Logging;
using Npgsql;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CashFlow.Consolidation.Infrastructure;

public sealed class RabbitMqTransactionConsumer(string uri, ITransactionProjection projection,
    ILogger<RabbitMqTransactionConsumer> logger)
{
    public const string MainQueue = "consolidation.transactions.v1";
    public const string ErrorQueue = "consolidation.transactions.v1.dlq";
    private const string MainExchange = "cashflow.events";
    private const string MainRoute = "transaction.created.v1";
    private const string RetryExchange = "cashflow.retry";
    private const string ErrorExchange = "cashflow.errors";
    private const string ErrorRoute = "transaction.created.v1.failed";
    private static readonly int[] Delays = [5, 15, 30, 60];
    private readonly ConnectionFactory _factory = new()
    {
        Uri = new Uri(uri),
        AutomaticRecoveryEnabled = false,
        ConsumerDispatchConcurrency = 8
    };
    private volatile bool _ready;

    public bool Ready => _ready;

    public async Task RunAsync(CancellationToken stoppingToken)
    {
        var backoffSeconds = 1;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await projection.ProbeAsync(stoppingToken);
                await using var connection = await _factory.CreateConnectionAsync(stoppingToken);
                await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
                await InitializeTopologyAsync(channel, stoppingToken);
                await ConfigureMainQueuePolicyAsync(stoppingToken);
                await channel.BasicQosAsync(0, 16, false, stoppingToken);
                var paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                connection.ConnectionShutdownAsync += (_, _) =>
                {
                    paused.TrySetResult();
                    return Task.CompletedTask;
                };
                using var concurrency = new SemaphoreSlim(8, 8);
                using var acknowledgements = new SemaphoreSlim(1, 1);
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, delivery) =>
                {
                    await concurrency.WaitAsync(stoppingToken);
                    try
                    {
                        if (paused.Task.IsCompleted) return;
                        await ProcessAsync(connection, channel, acknowledgements, delivery, paused, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
                    catch (Exception error)
                    {
                        logger.LogWarning(error, "Consumer paused with delivery unacknowledged.");
                        paused.TrySetResult();
                    }
                    finally { concurrency.Release(); }
                };
                var consumerTag = await channel.BasicConsumeAsync(MainQueue, autoAck: false, consumer,
                    cancellationToken: stoppingToken);
                _ready = true;
                backoffSeconds = 1;
                await Task.WhenAny(paused.Task, Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken));
                _ready = false;
                if (!stoppingToken.IsCancellationRequested)
                    await channel.BasicCancelAsync(consumerTag, cancellationToken: stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error)
            {
                _ready = false;
                logger.LogWarning(error, "Consolidation consumer dependency unavailable.");
            }
            if (stoppingToken.IsCancellationRequested) break;
            await Task.Delay(TimeSpan.FromSeconds(backoffSeconds), stoppingToken);
            backoffSeconds = Math.Min(60, backoffSeconds * 2);
        }
        _ready = false;
    }

    private async Task ProcessAsync(IConnection connection, IChannel channel, SemaphoreSlim acknowledgements,
        BasicDeliverEventArgs delivery, TaskCompletionSource paused, CancellationToken token)
    {
        try
        {
            var fact = TransactionCreatedFact.Parse(delivery.Body);
            await projection.ApplyAsync(fact, token);
            await AcknowledgeAsync(channel, acknowledgements, delivery.DeliveryTag, token);
        }
        catch (Exception error) when (error is InvalidTransactionEventException or FinancialIdentityConflictException)
        {
            await ForwardAndAcknowledgeAsync(connection, channel, acknowledgements, delivery,
                ErrorExchange, ErrorRoute, null, token);
            logger.LogError(error, "Event {MessageId} sent to DLQ.", delivery.BasicProperties.MessageId);
        }
        catch (Exception error) when (error is ProjectionInMaintenanceException or NpgsqlException or TimeoutException or OperationCanceledException)
        {
            if (error is PostgresException { SqlState: "22003" })
            {
                await RetryOrDeadLetterAsync(connection, channel, acknowledgements, delivery, token);
                return;
            }
            _ready = false;
            paused.TrySetResult();
            logger.LogWarning(error, "Projection unavailable; receipt remains unacknowledged.");
        }
        catch (Exception error)
        {
            logger.LogWarning(error, "Isolated processing failure for {MessageId}.",
                delivery.BasicProperties.MessageId);
            await RetryOrDeadLetterAsync(connection, channel, acknowledgements, delivery, token);
        }
    }

    private async Task RetryOrDeadLetterAsync(IConnection connection, IChannel channel,
        SemaphoreSlim acknowledgements, BasicDeliverEventArgs delivery, CancellationToken token)
    {
        var attempt = ReadAttempt(delivery.BasicProperties.Headers);
        if (attempt >= Delays.Length)
            await ForwardAndAcknowledgeAsync(connection, channel, acknowledgements, delivery,
                ErrorExchange, ErrorRoute, 5, token);
        else
            await ForwardAndAcknowledgeAsync(connection, channel, acknowledgements, delivery,
                RetryExchange, $"retry.{Delays[attempt]}s", attempt + 1, token);
    }

    private static int ReadAttempt(IDictionary<string, object?>? headers)
    {
        if (headers is null || !headers.TryGetValue("retryAttempt", out var value)) return 0;
        return value switch
        {
            int number when number is >= 1 and <= 5 => number,
            long number when number is >= 1 and <= 5 => (int)number,
            byte[] bytes when int.TryParse(Encoding.UTF8.GetString(bytes), out var number) && number is >= 1 and <= 5 => number,
            _ => 5
        };
    }

    private static async Task ForwardAndAcknowledgeAsync(IConnection connection, IChannel consumeChannel,
        SemaphoreSlim acknowledgements, BasicDeliverEventArgs delivery, string exchange, string routingKey,
        int? retryAttempt, CancellationToken token)
    {
        await using var publishChannel = await connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true), token);
        var headers = delivery.BasicProperties.Headers is null
            ? new Dictionary<string, object?>()
            : new Dictionary<string, object?>(delivery.BasicProperties.Headers);
        if (retryAttempt.HasValue) headers["retryAttempt"] = retryAttempt.Value;
        var properties = new BasicProperties
        {
            ContentType = delivery.BasicProperties.ContentType,
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = delivery.BasicProperties.MessageId,
            Type = delivery.BasicProperties.Type,
            CorrelationId = delivery.BasicProperties.CorrelationId,
            Headers = headers
        };
        await publishChannel.BasicPublishAsync(exchange, routingKey, mandatory: true,
            basicProperties: properties, body: delivery.Body, cancellationToken: token);
        await AcknowledgeAsync(consumeChannel, acknowledgements, delivery.DeliveryTag, token);
    }

    private static async Task AcknowledgeAsync(IChannel channel, SemaphoreSlim gate, ulong tag,
        CancellationToken token)
    {
        await gate.WaitAsync(token);
        try { await channel.BasicAckAsync(tag, multiple: false, cancellationToken: token); }
        finally { gate.Release(); }
    }

    private static async Task InitializeTopologyAsync(IChannel channel, CancellationToken token)
    {
        await channel.ExchangeDeclareAsync(MainExchange, ExchangeType.Direct, durable: true,
            cancellationToken: token);
        await channel.ExchangeDeclareAsync(RetryExchange, ExchangeType.Direct, durable: true,
            cancellationToken: token);
        await channel.ExchangeDeclareAsync(ErrorExchange, ExchangeType.Direct, durable: true,
            cancellationToken: token);
        await channel.QueueDeclareAsync(MainQueue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum",
                ["x-max-length-bytes"] = 1_073_741_824L,
                ["x-overflow"] = "reject-publish"
            }, cancellationToken: token);
        await channel.QueueBindAsync(MainQueue, MainExchange, MainRoute, cancellationToken: token);
        await channel.QueueDeclareAsync(ErrorQueue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum",
                ["x-max-length-bytes"] = 1_073_741_824L,
                ["x-overflow"] = "reject-publish"
            }, cancellationToken: token);
        await channel.QueueBindAsync(ErrorQueue, ErrorExchange, ErrorRoute, cancellationToken: token);
        foreach (var seconds in Delays)
        {
            var queue = $"consolidation.retry.{seconds}s";
            await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false,
                arguments: new Dictionary<string, object?>
                {
                    ["x-queue-type"] = "quorum",
                    ["x-message-ttl"] = seconds * 1_000,
                    ["x-dead-letter-exchange"] = MainExchange,
                    ["x-dead-letter-routing-key"] = MainRoute,
                    ["x-dead-letter-strategy"] = "at-least-once",
                    ["x-max-length-bytes"] = 1_073_741_824L,
                    ["x-overflow"] = "reject-publish"
                }, cancellationToken: token);
            await channel.QueueBindAsync(queue, RetryExchange, $"retry.{seconds}s", cancellationToken: token);
        }
    }

    private async Task ConfigureMainQueuePolicyAsync(CancellationToken token)
    {
        var amqp = new Uri(uri);
        var credentials = Uri.UnescapeDataString(amqp.UserInfo);
        if (string.IsNullOrEmpty(credentials) || !credentials.Contains(':'))
            throw new InvalidOperationException("RabbitMQ management credentials are required.");
        var vhost = Uri.UnescapeDataString(amqp.AbsolutePath.TrimStart('/'));
        if (string.IsNullOrEmpty(vhost)) vhost = "/";
        var endpoint = new UriBuilder("http", amqp.Host, 15672,
            $"/api/policies/{Uri.EscapeDataString(vhost)}/consolidation-main-protection").Uri;
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        using var request = new HttpRequestMessage(HttpMethod.Put, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials)));
        request.Content = new StringContent(JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["pattern"] = "^consolidation\\.transactions\\.v1$",
            ["priority"] = 10,
            ["apply-to"] = "queues",
            ["definition"] = new Dictionary<string, object>
            {
                ["dead-letter-exchange"] = ErrorExchange,
                ["dead-letter-routing-key"] = ErrorRoute,
                ["dead-letter-strategy"] = "at-least-once",
                ["delivery-limit"] = 100
            }
        }), Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request, token);
        response.EnsureSuccessStatusCode();
    }
}
