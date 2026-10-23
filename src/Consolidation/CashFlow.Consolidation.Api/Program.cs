using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CashFlow.Consolidation.Application;
using CashFlow.Consolidation.Infrastructure;
using CashFlow.Observability;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
builder.AddCashFlowObservability("cashflow-consolidation-api");
var authority = builder.Configuration["Authentication:Authority"] ?? throw new InvalidOperationException("Authentication:Authority is required.");
var issuer = builder.Configuration["Authentication:Issuer"] ?? throw new InvalidOperationException("Authentication:Issuer is required.");
var audience = builder.Configuration["Authentication:Audience"] ?? throw new InvalidOperationException("Authentication:Audience is required.");
var allowedClients = (builder.Configuration["Authentication:AllowedClientIds"] ?? throw new InvalidOperationException("Authentication:AllowedClientIds is required."))
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal);
if (allowedClients.Count == 0) throw new InvalidOperationException("At least one allowed client ID is required.");
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["correlationId"] = context.HttpContext.Items["CorrelationId"]?.ToString());
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.Authority = authority;
    options.Audience = audience;
    options.RequireHttpsMetadata = builder.Configuration.GetValue("Authentication:RequireHttpsMetadata", true);
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = issuer, ValidateAudience = true, ValidAudience = audience,
        ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true,
        ValidAlgorithms = [SecurityAlgorithms.RsaSha256], ClockSkew = TimeSpan.FromSeconds(60)
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = context =>
        {
            var clientId = context.Principal?.FindFirst("client_id")?.Value;
            if (string.IsNullOrWhiteSpace(clientId) || !allowedClients.Contains(clientId))
                context.Fail("Client ID is missing or not allowed.");
            return Task.CompletedTask;
        },
        OnChallenge = async context =>
        {
            context.HandleResponse();
            await Results.Problem(statusCode: 401, title: "Unauthorized", detail: "A valid access token is required.")
                .ExecuteAsync(context.HttpContext);
        },
        OnForbidden = async context =>
            await Results.Problem(statusCode: 403, title: "Forbidden", detail: "Required permission is missing.")
                .ExecuteAsync(context.HttpContext)
    };
});
builder.Services.AddAuthorization(options => options.AddPolicy("consolidations.read", policy =>
    policy.RequireAuthenticatedUser().RequireAssertion(context =>
        ConsolidationPermissionAdapter.CanRead(context.User))));
builder.Services.AddSingleton(NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("Consolidation") ??
    throw new InvalidOperationException("ConnectionStrings:Consolidation is required.")));
builder.Services.AddScoped<IConsolidationQuery, PostgresConsolidationQuery>();
builder.Services.AddHealthChecks().AddCheck("self", () => HealthCheckResult.Healthy(), ["live", "ready"])
    .AddCheck<ConsolidationHealthCheck>("postgresql", tags: ["ready"]);
var app = builder.Build();
app.Use(async (context, next) =>
{
    var correlationId = Guid.TryParse(context.Request.Headers["X-Correlation-Id"], out var id)
        ? id.ToString() : Guid.NewGuid().ToString();
    context.Items["CorrelationId"] = correlationId;
    context.Response.OnStarting(() =>
    {
        context.Response.Headers["X-Correlation-Id"] = correlationId;
        context.Response.Headers.CacheControl = "no-store";
        return Task.CompletedTask;
    });
    await next(context);
});
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    ExceptionHandler = async context =>
    {
        var error = context.Features.Get<IExceptionHandlerFeature>()!.Error;
        app.Logger.LogError(error, "Consolidation request failed.");
        var unavailable = error is NpgsqlException and not PostgresException ||
            error is PostgresException postgres &&
            (postgres.SqlState.StartsWith("08", StringComparison.Ordinal) || postgres.SqlState is "57P01" or "57P02" or "57P03");
        await Results.Problem(statusCode: unavailable ? 503 : 500,
            title: unavailable ? "Service unavailable" : "Internal error",
            detail: unavailable ? "Consolidation persistence is unavailable." : "The request could not be completed.")
            .ExecuteAsync(context);
    }
});
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = x => x.Tags.Contains("live") });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = x => x.Tags.Contains("ready") });
app.MapCashFlowMetrics();
app.MapGet("/consolidations/{date}", async (string date, IConsolidationQuery query, CancellationToken token) =>
{
    if (!TryDate(date, out var day)) return Problem(400, "Invalid date.");
    var page = await query.GetAsync(day, day, token);
    return Results.Ok(ToDto(day, page.Days.SingleOrDefault(), page));
}).RequireAuthorization("consolidations.read");
app.MapGet("/consolidations", async (HttpContext http, IConsolidationQuery query, CancellationToken token) =>
{
    var values = http.Request.Query;
    if (!TryDate(values["from"], out var from) || !TryDate(values["to"], out var to) ||
        to < from || to.DayNumber - from.DayNumber > 365)
        return Problem(400, "Invalid range.");
    var pageSize = 50;
    if (values.ContainsKey("pageSize") && (!int.TryParse(values["pageSize"], out pageSize) || pageSize is < 1 or > 200))
        return Problem(400, "Invalid pageSize.");
    ConsolidationCursor? cursor = null;
    if (values.ContainsKey("cursor"))
    {
        try
        {
            cursor = JsonSerializer.Deserialize<ConsolidationCursor>(Convert.FromBase64String(values["cursor"].ToString()));
            if (cursor is null || cursor.GenerationId == Guid.Empty || cursor.From != from || cursor.To != to ||
                cursor.PageSize != pageSize || cursor.LastBusinessDate < from || cursor.LastBusinessDate >= to)
                return Problem(400, "Cursor does not match this range and page size.");
        }
        catch (Exception error) when (error is FormatException or JsonException or OverflowException)
        {
            return Problem(400, "Invalid cursor.");
        }
    }
    var first = cursor?.LastBusinessDate.AddDays(1) ?? from;
    var last = DateOnly.FromDayNumber(Math.Min(to.DayNumber, first.DayNumber + pageSize - 1));
    var page = await query.GetAsync(first, last, token);
    if (cursor is not null && cursor.GenerationId != page.GenerationId)
        return Results.Problem(statusCode: 409, type: "projection-generation-changed",
            title: "Projection generation changed", detail: "Restart pagination from the first page.");
    var byDate = page.Days.ToDictionary(day => day.BusinessDate);
    var items = Enumerable.Range(first.DayNumber, last.DayNumber - first.DayNumber + 1)
        .Select(number =>
        {
            var day = DateOnly.FromDayNumber(number);
            return ToDto(day, byDate.GetValueOrDefault(day), page);
        }).ToArray();
    var nextCursor = last < to ? Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(
        new ConsolidationCursor(page.GenerationId, from, to, pageSize, last))) : null;
    return Results.Ok(new { items, nextCursor, generationId = page.GenerationId,
        projectionState = page.Maintenance ? "REBUILDING" : "AVAILABLE",
        consistency = "EVENTUAL", completeness = "UNKNOWN" });
}).RequireAuthorization("consolidations.read");
app.Run();

static bool TryDate(string? value, out DateOnly date) =>
    DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
static IResult Problem(int status, string detail) => Results.Problem(statusCode: status, title: "Request failed", detail: detail);
static object ToDto(DateOnly date, DailyConsolidation? row, ConsolidationPage page)
{
    var credits = row?.TotalCredits ?? 0m;
    var debits = row?.TotalDebits ?? 0m;
    return new
    {
        businessDate = date, currency = "BRL",
        totalCredits = credits.ToString("F2", CultureInfo.InvariantCulture),
        totalDebits = debits.ToString("F2", CultureInfo.InvariantCulture),
        balance = (credits - debits).ToString("F2", CultureInfo.InvariantCulture),
        transactionCount = row?.TransactionCount ?? 0L,
        lastUpdatedAt = row?.LastUpdatedAt?.UtcDateTime, generationId = page.GenerationId,
        consistency = "EVENTUAL", projectionState = page.Maintenance ? "REBUILDING" : "AVAILABLE",
        completeness = "UNKNOWN"
    };
}
internal sealed record ConsolidationCursor(Guid GenerationId, DateOnly From, DateOnly To, int PageSize,
    DateOnly LastBusinessDate);
internal static class ConsolidationPermissionAdapter
{
    public static bool CanRead(ClaimsPrincipal principal) =>
        principal.FindAll("permissions").Any(claim => claim.Value == "consolidations.read") ||
        principal.FindAll("scope").Any(claim => claim.Value.Split(' ').Contains("cashflow/consolidations.read"));
}
internal sealed class ConsolidationHealthCheck(IConsolidationQuery query) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await query.ProbeAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception error) when (error is NpgsqlException or InvalidOperationException)
        {
            return HealthCheckResult.Unhealthy();
        }
    }
}
public partial class Program;
