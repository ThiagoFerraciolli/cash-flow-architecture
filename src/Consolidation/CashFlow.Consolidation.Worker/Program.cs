using CashFlow.Consolidation.Application;
using CashFlow.Consolidation.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
builder.Host.ConfigureHostOptions(options => options.ShutdownTimeout = TimeSpan.FromSeconds(30));
builder.Services.AddSingleton(NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("Consolidation") ??
    throw new InvalidOperationException("ConnectionStrings:Consolidation is required.")));
builder.Services.AddSingleton<ITransactionProjection, PostgresTransactionProjection>();
builder.Services.AddSingleton<RabbitMqTransactionConsumer>(provider => new RabbitMqTransactionConsumer(
    builder.Configuration["RabbitMq:Uri"] ?? throw new InvalidOperationException("RabbitMq:Uri is required."),
    provider.GetRequiredService<ITransactionProjection>(),
    provider.GetRequiredService<ILogger<RabbitMqTransactionConsumer>>()));
builder.Services.AddHostedService<ConsolidationConsumerService>();
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), ["live", "ready"])
    .AddCheck<ConsolidationReadinessCheck>("consumer", tags: ["ready"]);

var app = builder.Build();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("live")
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
});
app.Run();

internal sealed class ConsolidationConsumerService(RabbitMqTransactionConsumer consumer) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => consumer.RunAsync(stoppingToken);
}

internal sealed class ConsolidationReadinessCheck(RabbitMqTransactionConsumer consumer,
    ITransactionProjection projection) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!consumer.Ready) return HealthCheckResult.Unhealthy("Consumer is paused.");
        try
        {
            await projection.ProbeAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception error) { return HealthCheckResult.Unhealthy("Projection is unavailable.", error); }
    }
}

public partial class Program;
