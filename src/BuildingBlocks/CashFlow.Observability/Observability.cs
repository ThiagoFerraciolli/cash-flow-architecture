using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace CashFlow.Observability;

public static class CashFlowTelemetry
{
    public const string SourceName = "CashFlow.Observability";
    public static readonly ActivitySource ActivitySource = new(SourceName);
    private static readonly Meter Meter = new(SourceName);
    public static readonly Counter<long> OutboxPublished = Meter.CreateCounter<long>("cashflow.outbox.published");
    public static readonly Counter<long> ConsumerApplied = Meter.CreateCounter<long>("cashflow.consolidation.applied");
    public static readonly Counter<long> ConsumerDuplicate = Meter.CreateCounter<long>("cashflow.consolidation.duplicate");
    public static readonly Counter<long> ConsumerDeadLettered = Meter.CreateCounter<long>("cashflow.consolidation.dead_lettered");

    public static Activity? StartFromHeaders(string name, ActivityKind kind,
        IReadOnlyDictionary<string, string> headers)
    {
        if (headers.TryGetValue("traceparent", out var parent) && parent.Length <= 128 &&
            headers.GetValueOrDefault("tracestate") is { Length: <= 512 } state &&
            ActivityContext.TryParse(parent, state, isRemote: true, out var context))
            return ActivitySource.StartActivity(name, kind, context);
        if (headers.TryGetValue("traceparent", out parent) && parent.Length <= 128 &&
            ActivityContext.TryParse(parent, null, isRemote: true, out context))
            return ActivitySource.StartActivity(name, kind, context);
        return ActivitySource.StartActivity(name, kind);
    }

    public static void AddCashFlowObservability(this WebApplicationBuilder builder, string serviceName)
    {
        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
        builder.Logging.Configure(options => options.ActivityTrackingOptions =
            ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId);

        var endpoint = builder.Configuration["Observability:OtlpEndpoint"];
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing.AddSource(SourceName)
                    .AddAspNetCoreInstrumentation(options =>
                        options.Filter = context => !context.Request.Path.StartsWithSegments("/health") &&
                            !context.Request.Path.StartsWithSegments("/metrics"))
                    .AddHttpClientInstrumentation()
                    .AddNpgsql();
                if (!string.IsNullOrWhiteSpace(endpoint))
                    tracing.AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(endpoint);
                        options.Protocol = OtlpExportProtocol.HttpProtobuf;
                    });
            })
            .WithMetrics(metrics => metrics
                .AddMeter(SourceName)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddPrometheusExporter());
    }

    public static void MapCashFlowMetrics(this WebApplication app) =>
        app.MapPrometheusScrapingEndpoint("/metrics");
}
