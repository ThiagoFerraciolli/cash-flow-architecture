using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CashFlow.Transactions.Application;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace CashFlow.Transactions.Infrastructure;

public sealed class RabbitMqOutboxPublisher(string uri, string exchange = "cashflow.events",
    string routingKey = "transaction.created.v1", string queue = "consolidation.transactions.v1")
    : IOutboxEventPublisher, IAsyncDisposable
{
    private readonly ConnectionFactory _factory = new() { Uri = new Uri(uri), AutomaticRecoveryEnabled = true };
    private readonly SemaphoreSlim _connectionGate = new(1, 1);
    private IConnection? _connection;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var connection = await ConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(exchange, ExchangeType.Direct, durable: true,
            autoDelete: false, cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(queue, durable: true, exclusive: false,
            autoDelete: false, arguments: new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum",
                ["x-max-length-bytes"] = 1_073_741_824L,
                ["x-overflow"] = "reject-publish"
            }, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(queue, exchange, routingKey, cancellationToken: cancellationToken);
    }

    public async Task PublishAsync(OutboxLease lease, CancellationToken cancellationToken)
    {
        Validate(lease);
        var connection = await ConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true), cancellationToken);
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = lease.EventId.ToString(),
            Type = "TransactionCreatedV1",
            CorrelationId = lease.Headers.GetValueOrDefault("correlation-id"),
            Headers = lease.Headers.Where(pair => pair.Key is "traceparent" or "tracestate")
                .ToDictionary(pair => pair.Key, pair => (object?)Encoding.UTF8.GetBytes(pair.Value))
        };
        // With confirmation tracking, this await fails on nack or mandatory return.
        try
        {
            await channel.BasicPublishAsync(exchange, routingKey, mandatory: true,
                basicProperties: properties, body: Encoding.UTF8.GetBytes(lease.Payload),
                cancellationToken: cancellationToken);
        }
        catch (PublishException error) when (error.IsReturn)
        {
            throw new UnroutableOutboxEventException("RabbitMQ returned an unroutable message.");
        }
    }

    public async Task ProbeAsync(CancellationToken cancellationToken)
    {
        var connection = await ConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
    }

    private async Task<IConnection> ConnectionAsync(CancellationToken cancellationToken)
    {
        await _connectionGate.WaitAsync(cancellationToken);
        try
        {
            if (_connection?.IsOpen == true) return _connection;
            if (_connection is not null) await _connection.DisposeAsync();
            _connection = null;
            _connection = await _factory.CreateConnectionAsync(cancellationToken);
            return _connection;
        }
        finally { _connectionGate.Release(); }
    }

    private static void Validate(OutboxLease lease)
    {
        if (Encoding.UTF8.GetByteCount(lease.Payload) > 16 * 1024)
            throw new InvalidOutboxEventException("Outbox payload exceeds 16 KiB.");
        try
        {
            using var document = JsonDocument.Parse(lease.Payload);
            var root = document.RootElement;
            if (root.GetProperty("schemaVersion").GetInt32() != 1 ||
                root.GetProperty("eventType").GetString() != "TransactionCreated" ||
                root.GetProperty("eventId").GetGuid() != lease.EventId ||
                root.GetProperty("transactionId").GetGuid() != lease.TransactionId ||
                root.GetProperty("transactionType").GetString() is not ("CREDIT" or "DEBIT") ||
                !Regex.IsMatch(root.GetProperty("amount").GetString() ?? "", @"^(0|[1-9][0-9]*)\.[0-9]{2}$") ||
                !DateOnly.TryParseExact(root.GetProperty("businessDate").GetString(), "yyyy-MM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out _) ||
                !DateTimeOffset.TryParse(root.GetProperty("eventOccurredAt").GetString(),
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var occurred) ||
                occurred.Offset != TimeSpan.Zero)
                throw new InvalidOutboxEventException("Outbox event does not match TransactionCreatedV1.");
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or FormatException or KeyNotFoundException)
        {
            throw new InvalidOutboxEventException("Outbox event is invalid.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _connectionGate.WaitAsync();
        try { if (_connection is not null) await _connection.DisposeAsync(); }
        finally { _connectionGate.Release(); _connectionGate.Dispose(); }
    }
}
