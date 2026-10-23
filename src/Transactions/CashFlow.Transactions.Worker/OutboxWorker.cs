using CashFlow.Transactions.Application;
using CashFlow.Observability;
using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CashFlow.Transactions.Worker;

public sealed class OutboxWorker(IOutboxLeaseStore store, IOutboxEventPublisher publisher,
    ILogger<OutboxWorker> logger) : BackgroundService
{
    private readonly object _circuitGate = new();
    private DateTimeOffset _resumeAt;
    private int _systemicFailures;
    private bool _topologyReady;
    private volatile bool _ready;

    public bool Ready => _ready;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunBatchAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error)
            {
                _ready = false;
                _topologyReady = false;
                OpenCircuit();
                logger.LogWarning(error, "Outbox worker dependency unavailable; probing after backoff.");
            }
        }
    }

    public async Task RunBatchAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset resumeAt;
        lock (_circuitGate) resumeAt = _resumeAt;
        var pause = resumeAt - DateTimeOffset.UtcNow;
        if (pause > TimeSpan.Zero)
        {
            await Task.Delay(pause, cancellationToken);
            return;
        }
        using var probeTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        probeTimeout.CancelAfter(TimeSpan.FromSeconds(10));
        if (!_topologyReady)
        {
            await publisher.InitializeAsync(probeTimeout.Token);
            _topologyReady = true;
        }
        else await publisher.ProbeAsync(probeTimeout.Token);
        var batch = await store.ClaimAsync(20, cancellationToken);
        _ready = true;
        await Parallel.ForEachAsync(batch,
            new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = cancellationToken },
            async (lease, token) => await ProcessAsync(lease, token));
    }

    private async ValueTask ProcessAsync(OutboxLease lease, CancellationToken cancellationToken)
    {
        DateTimeOffset resumeAt;
        lock (_circuitGate) resumeAt = _resumeAt;
        if (resumeAt > DateTimeOffset.UtcNow)
        {
            await store.MarkFailureAsync(lease, OutboxFailureKind.Systemic,
                Backoff(_systemicFailures), "broker-unavailable", cancellationToken);
            return;
        }

        using var publishTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        publishTimeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var renewCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var renewal = RenewUntilDoneAsync(lease, renewCancellation.Token);
        try
        {
            using (var activity = CashFlowTelemetry.StartFromHeaders(
                "outbox publish", ActivityKind.Producer, lease.Headers))
            {
                activity?.SetTag("messaging.system", "rabbitmq");
                activity?.SetTag("messaging.message.id", lease.EventId.ToString());
                await publisher.PublishAsync(lease, publishTimeout.Token);
                CashFlowTelemetry.OutboxPublished.Add(1);
            }
            if (!await store.MarkPublishedAsync(lease, cancellationToken))
                logger.LogWarning("Confirmed event {EventId} lost its lease before marking published; replay remains possible.", lease.EventId);
            else CloseCircuit();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (InvalidOutboxEventException)
        {
            await store.MarkFailureAsync(lease, OutboxFailureKind.Permanent, TimeSpan.Zero,
                "invalid-event", cancellationToken);
            logger.LogError("Outbox event {EventId} is suspended: invalid contract.", lease.EventId);
        }
        catch (Exception error) when (error is not Npgsql.NpgsqlException)
        {
            var systemic = error is UnroutableOutboxEventException or OperationCanceledException or TimeoutException
                or System.IO.IOException or System.Net.Sockets.SocketException
                or RabbitMQ.Client.Exceptions.BrokerUnreachableException
                or RabbitMQ.Client.Exceptions.OperationInterruptedException
                or RabbitMQ.Client.Exceptions.AlreadyClosedException
                or RabbitMQ.Client.Exceptions.PublishException;
            if (systemic)
            {
                _ready = false;
                _topologyReady = false;
                OpenCircuit();
            }
            var kind = systemic ? OutboxFailureKind.Systemic : OutboxFailureKind.Isolated;
            var delay = Backoff(systemic ? _systemicFailures : lease.Attempts + 1);
            await store.MarkFailureAsync(lease, kind, delay,
                error is UnroutableOutboxEventException ? "unroutable" : systemic ? "broker-unavailable" : "isolated-publish-failure",
                cancellationToken);
            logger.LogWarning(error, "Outbox event {EventId} publication failed ({Kind}).", lease.EventId, kind);
        }
        finally
        {
            await renewCancellation.CancelAsync();
            try { await renewal; }
            catch (OperationCanceledException) { }
        }
    }

    private async Task RenewUntilDoneAsync(OutboxLease lease, CancellationToken cancellationToken)
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(20), cancellationToken);
            if (!await store.RenewAsync(lease, cancellationToken)) return;
        }
    }

    private void OpenCircuit()
    {
        lock (_circuitGate)
        {
            _systemicFailures = Math.Min(_systemicFailures + 1, 7);
            _resumeAt = DateTimeOffset.UtcNow + Backoff(_systemicFailures);
        }
    }

    private void CloseCircuit()
    {
        lock (_circuitGate)
        {
            _systemicFailures = 0;
            _resumeAt = DateTimeOffset.MinValue;
        }
        _ready = true;
    }

    private static TimeSpan Backoff(int failures) => TimeSpan.FromMilliseconds(
        Math.Min(60_000, Math.Pow(2, Math.Max(0, failures - 1)) * 1_000) *
        (0.8 + Random.Shared.NextDouble() * 0.2));
}
