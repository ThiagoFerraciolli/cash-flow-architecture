using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CashFlow.Transactions.Application;
using CashFlow.Transactions.Domain;
using CashFlow.Transactions.Infrastructure;
using CashFlow.Observability;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.AddCashFlowObservability("cashflow-transactions-api");
var authority = builder.Configuration["Authentication:Authority"] ?? throw new InvalidOperationException("Authentication:Authority is required.");
var issuer = builder.Configuration["Authentication:Issuer"] ?? throw new InvalidOperationException("Authentication:Issuer is required.");
var audience = builder.Configuration["Authentication:Audience"] ?? throw new InvalidOperationException("Authentication:Audience is required.");
var allowedClients = (builder.Configuration["Authentication:AllowedClientIds"] ?? throw new InvalidOperationException("Authentication:AllowedClientIds is required."))
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal);
if (allowedClients.Count == 0) throw new InvalidOperationException("At least one allowed client ID is required.");
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 16 * 1024);
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["correlationId"] = context.HttpContext.Items["CorrelationId"]?.ToString());
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.Authority = authority;
    options.Audience = audience;
    options.RequireHttpsMetadata = builder.Configuration.GetValue("Authentication:RequireHttpsMetadata", true);
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = issuer,
        ValidateAudience = true,
        ValidAudience = audience,
        ValidateLifetime = true,
        RequireExpirationTime = true,
        RequireSignedTokens = true,
        ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
        ClockSkew = TimeSpan.FromSeconds(60)
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = context =>
        {
            var clientId = context.Principal?.FindFirst("client_id")?.Value;
            if (string.IsNullOrWhiteSpace(clientId) || !allowedClients.Contains(clientId))
                context.Fail("Client ID is missing or not allowed.");
            return Task.CompletedTask;
        }
    };
});
builder.Services.AddAuthorization(options => options.AddPolicy("transactions.write", policy =>
    policy.RequireAuthenticatedUser().RequireAssertion(context =>
        context.User.FindAll("permissions").Any(claim => claim.Value == "transactions.write") ||
        context.User.FindAll("scope").Any(claim => claim.Value.Split(' ').Contains("cashflow/transactions.write")))));
builder.Services.AddSingleton<ValidatedClientIdentityAdapter>();
builder.Services.AddSingleton(TimeZoneInfo.FindSystemTimeZoneById(builder.Configuration["Transactions:TimeZone"] ?? "America/Sao_Paulo"));
builder.Services.AddSingleton(NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("Transactions") ??
    throw new InvalidOperationException("ConnectionStrings:Transactions is required.")));
builder.Services.AddScoped<ITransactionRepository, PostgresTransactionRepository>();
builder.Services.AddScoped<ITransactionCreationStore, PostgresTransactionCreationStore>();
builder.Services.AddScoped<TransactionService>();
builder.Services.AddHealthChecks().AddCheck("self", () => HealthCheckResult.Healthy(), ["live", "ready"])
    .AddCheck<PostgresHealthCheck>("postgresql", tags: ["ready"]);
var app = builder.Build();
app.Use(async (context, next) =>
{
    var correlationId = Guid.TryParse(context.Request.Headers["X-Correlation-Id"], out var id) ? id.ToString() : Guid.NewGuid().ToString();
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
    StatusCodeSelector = error => IsPersistenceUnavailable(error)
        ? StatusCodes.Status503ServiceUnavailable
        : StatusCodes.Status500InternalServerError
});
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = x => x.Tags.Contains("live") });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = x => x.Tags.Contains("ready") });
app.MapCashFlowMetrics();

app.MapPost("/transactions", async (HttpContext http, TransactionService service,
    ValidatedClientIdentityAdapter identityAdapter, CancellationToken ct) =>
{
    if (http.Request.ContentLength > 16 * 1024) return Problem(413, "Body exceeds 16 KiB.");
    if (!Guid.TryParse(http.Request.Headers["Idempotency-Key"], out var idempotencyKey)) return Problem(400, "Invalid Idempotency-Key.");
    try
    {
        using var document = await JsonDocument.ParseAsync(http.Request.Body, new JsonDocumentOptions { MaxDepth = 8 }, ct);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Any(p => p.Name is not ("type" or "amount" or "occurredAt" or "description")))
            return Problem(400, "Invalid fields.");
        if (!root.TryGetProperty("type", out var typeJson) || typeJson.ValueKind != JsonValueKind.String || !TryType(typeJson.GetString(), out var type))
            return Problem(400, "Invalid type.");
        if (!root.TryGetProperty("amount", out var amountJson) || amountJson.ValueKind != JsonValueKind.String ||
            !System.Text.RegularExpressions.Regex.IsMatch(amountJson.GetString() ?? "", @"^(0|[1-9][0-9]*)\.[0-9]{2}$") ||
            !decimal.TryParse(amountJson.GetString(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount))
            return Problem(400, "Invalid amount.");
        if (!root.TryGetProperty("occurredAt", out var occurredJson) || occurredJson.ValueKind != JsonValueKind.String ||
            !DateTimeOffset.TryParse(occurredJson.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var occurredAt) ||
            !System.Text.RegularExpressions.Regex.IsMatch(occurredJson.GetString() ?? "", @"(Z|[+-][0-9]{2}:[0-9]{2})$"))
            return Problem(400, "Invalid occurredAt.");
        string? description = null;
        if (root.TryGetProperty("description", out var descriptionJson))
        {
            if (descriptionJson.ValueKind is not (JsonValueKind.String or JsonValueKind.Null)) return Problem(400, "Invalid description.");
            description = descriptionJson.ValueKind == JsonValueKind.String ? descriptionJson.GetString() : null;
        }
        var result = await service.CreateAsync(identityAdapter.FromValidatedClaims(http.User), idempotencyKey,
            type, amount, description, occurredAt, http.Items["CorrelationId"]!.ToString()!, ct);
        if (result.Status == CreateTransactionStatus.Conflict) return Problem(409, "Idempotency key was used for another command.");
        var transaction = result.Transaction!;
        if (result.Status == CreateTransactionStatus.Replayed) http.Response.Headers["Idempotent-Replayed"] = "true";
        return Results.Created($"/transactions/{transaction.Id}", Dto(transaction));
    }
    catch (Exception error) when (error is JsonException or ArgumentException or OverflowException)
    {
        return Problem(400, "Invalid transaction.");
    }
}).RequireAuthorization("transactions.write");
app.MapGet("/transactions/{id:guid}", async (Guid id, TransactionService service, CancellationToken ct) =>
{
    var transaction = await service.GetAsync(id, ct);
    return transaction is null ? Problem(404, "Transaction not found.") : Results.Ok(Dto(transaction));
});
app.MapGet("/transactions", async (HttpContext http, TransactionService service, CancellationToken ct) =>
{
    var query = http.Request.Query;
    if (!DateOnly.TryParseExact(query["from"], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var from) ||
        !DateOnly.TryParseExact(query["to"], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var to) ||
        to < from || to.DayNumber - from.DayNumber > 366) return Problem(400, "Invalid range.");
    TransactionType? type = null;
    if (query.ContainsKey("type"))
    {
        if (!TryType(query["type"], out var parsed)) return Problem(400, "Invalid type.");
        type = parsed;
    }
    var pageSize = 50;
    if (query.ContainsKey("pageSize") && (!int.TryParse(query["pageSize"], out pageSize) || pageSize is < 1 or > 200))
        return Problem(400, "Invalid pageSize.");
    DateOnly? afterDate = null;
    Guid? afterId = null;
    if (query.ContainsKey("cursor"))
    {
        try
        {
            var cursor = JsonSerializer.Deserialize<Cursor>(Convert.FromBase64String(query["cursor"].ToString()));
            if (cursor is null || cursor.From != from || cursor.To != to || cursor.Type != type || cursor.PageSize != pageSize)
                return Problem(400, "Cursor does not match filters.");
            afterDate = cursor.Date;
            afterId = cursor.Id;
        }
        catch (Exception error) when (error is FormatException or JsonException) { return Problem(400, "Invalid cursor."); }
    }
    var rows = await service.ListAsync(from, to, type, afterDate, afterId, pageSize + 1, ct);
    var items = rows.Take(pageSize).ToArray();
    string? nextCursor = rows.Count > pageSize ? Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(
        new Cursor(from, to, type, pageSize, items[^1].BusinessDate, items[^1].Id))) : null;
    return Results.Ok(new { items = items.Select(Dto), nextCursor });
});
app.Run();

static IResult Problem(int status, string detail) => Results.Problem(statusCode: status, title: "Request failed", detail: detail);
static bool IsPersistenceUnavailable(Exception error) =>
    error is NpgsqlException and not PostgresException ||
    error is PostgresException postgres &&
    (postgres.SqlState.StartsWith("08", StringComparison.Ordinal) || postgres.SqlState is "57P01" or "57P02" or "57P03");
static bool TryType(string? value, out TransactionType type)
{
    type = value == "DEBIT" ? TransactionType.Debit : TransactionType.Credit;
    return value is "CREDIT" or "DEBIT";
}
static object Dto(FinancialTransaction value) => new { value.Id, type = value.Type.ToString().ToUpperInvariant(),
    amount = value.Amount.ToString("F2", CultureInfo.InvariantCulture), value.Description,
    occurredAt = value.OccurredAt.UtcDateTime, value.BusinessDate, createdAt = value.CreatedAt.UtcDateTime };
internal sealed record Cursor(DateOnly From, DateOnly To, TransactionType? Type, int PageSize, DateOnly Date, Guid Id);
internal sealed class PostgresHealthCheck(NpgsqlDataSource source) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var command = source.CreateCommand("SELECT 1");
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (NpgsqlException) { return HealthCheckResult.Unhealthy(); }
    }
}
public partial class Program;

internal sealed class ValidatedClientIdentityAdapter
{
    public AuthenticatedClientIdentity FromValidatedClaims(ClaimsPrincipal principal) => new(
        principal.FindFirst("iss")?.Value ?? throw new InvalidOperationException("Validated issuer is missing."),
        principal.FindFirst("client_id")?.Value ?? throw new InvalidOperationException("Validated client ID is missing."));
}
