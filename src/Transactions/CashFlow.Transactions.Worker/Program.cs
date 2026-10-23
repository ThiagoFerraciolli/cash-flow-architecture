using CashFlow.Transactions.Application;
using CashFlow.Transactions.Infrastructure;
using CashFlow.Transactions.Worker;
using CashFlow.Observability;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
builder.AddCashFlowObservability("cashflow-transactions-worker");
builder.Host.ConfigureHostOptions(options => options.ShutdownTimeout = TimeSpan.FromSeconds(30));
builder.Services.AddSingleton(NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("Transactions") ??
    throw new InvalidOperationException("ConnectionStrings:Transactions is required.")));
builder.Services.AddSingleton<IOutboxLeaseStore, PostgresOutboxLeaseStore>();
builder.Services.AddSingleton<RabbitMqOutboxPublisher>(_ => new RabbitMqOutboxPublisher(
    builder.Configuration["RabbitMq:Uri"] ?? throw new InvalidOperationException("RabbitMq:Uri is required.")));
builder.Services.AddSingleton<IOutboxEventPublisher>(provider => provider.GetRequiredService<RabbitMqOutboxPublisher>());
builder.Services.AddSingleton<OutboxWorker>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<OutboxWorker>());
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), ["live", "ready"])
    .AddCheck<WorkerReadinessCheck>("outbox-publisher", tags: ["ready"]);

var app = builder.Build();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = registration => registration.Tags.Contains("live") });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = registration => registration.Tags.Contains("ready") });
app.MapCashFlowMetrics();
app.Run();

internal sealed class WorkerReadinessCheck(OutboxWorker worker, NpgsqlDataSource source) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!worker.Ready) return HealthCheckResult.Unhealthy("Publisher is not ready.");
        try
        {
            await using var command = source.CreateCommand("SELECT 1 FROM outbox_messages LIMIT 1");
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception error) { return HealthCheckResult.Unhealthy("Outbox database is unavailable.", error); }
    }
}

partial class Program;
