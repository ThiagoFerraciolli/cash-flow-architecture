using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Npgsql;
using Xunit;

namespace CashFlow.Consolidation.IntegrationTests;

public sealed class ConsolidationApiTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Existing_empty_and_rebuilding_dates_return_coherent_public_values()
    {
        await ProjectionTests.WithDatabase(async source =>
        {
            var generation = await ActiveGeneration(source);
            await Seed(source, generation, "2026-10-22", 150m, 20m, 3);
            await Seed(source, generation, "2026-10-23", 1m, 2m, 3_000_000_000L);
            using var factory = ConsolidationApiTestHost.Create(FullConnectionString(source));
            using var client = factory.CreateClient();
            using (var response = await Get(client, "/consolidations/2026-10-22"))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
                Assert.True(Guid.TryParse(response.Headers.GetValues("X-Correlation-Id").Single(), out _));
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
                var root = json.RootElement;
                Assert.Equal("2026-10-22", root.GetProperty("businessDate").GetString());
                Assert.Equal("BRL", root.GetProperty("currency").GetString());
                Assert.Equal("150.00", root.GetProperty("totalCredits").GetString());
                Assert.Equal("20.00", root.GetProperty("totalDebits").GetString());
                Assert.Equal("130.00", root.GetProperty("balance").GetString());
                Assert.Equal(3, root.GetProperty("transactionCount").GetInt64());
                Assert.Equal(generation, root.GetProperty("generationId").GetGuid());
                Assert.Equal("AVAILABLE", root.GetProperty("projectionState").GetString());
                Assert.Equal("EVENTUAL", root.GetProperty("consistency").GetString());
                Assert.Equal("UNKNOWN", root.GetProperty("completeness").GetString());
                Assert.NotEqual(JsonValueKind.Null, root.GetProperty("lastUpdatedAt").ValueKind);
                Assert.EndsWith("Z", root.GetProperty("lastUpdatedAt").GetString());
            }
            using (var response = await Get(client, "/consolidations/2026-10-24"))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
                var root = json.RootElement;
                Assert.Equal("0.00", root.GetProperty("totalCredits").GetString());
                Assert.Equal("0.00", root.GetProperty("totalDebits").GetString());
                Assert.Equal("0.00", root.GetProperty("balance").GetString());
                Assert.Equal(0, root.GetProperty("transactionCount").GetInt64());
                Assert.Equal(JsonValueKind.Null, root.GetProperty("lastUpdatedAt").ValueKind);
            }
            using (var response = await Get(client, "/consolidations/2026-10-23"))
            using (var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token)))
                Assert.Equal(3_000_000_000L, json.RootElement.GetProperty("transactionCount").GetInt64());
            await Execute(source, "UPDATE projection_control SET maintenance = true");
            using (var response = await Get(client, "/consolidations/2026-10-22"))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
                Assert.Equal(generation, json.RootElement.GetProperty("generationId").GetGuid());
                Assert.Equal("REBUILDING", json.RootElement.GetProperty("projectionState").GetString());
                Assert.Equal("150.00", json.RootElement.GetProperty("totalCredits").GetString());
            }
            using var ready = await client.GetAsync("/health/ready", Token);
            Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        });
    }

    [Fact]
    public async Task Range_has_inclusive_zero_days_cursor_validation_and_generation_conflict()
    {
        await ProjectionTests.WithDatabase(async source =>
        {
            var generation = await ActiveGeneration(source);
            await Seed(source, generation, "2026-10-01", 10m, 0m, 1);
            await Seed(source, generation, "2026-10-03", 0m, 2m, 1);
            using var factory = ConsolidationApiTestHost.Create(FullConnectionString(source));
            using var client = factory.CreateClient();
            using var first = await Get(client, "/consolidations?from=2026-10-01&to=2026-10-04&pageSize=2");
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            using var firstJson = JsonDocument.Parse(await first.Content.ReadAsStringAsync(Token));
            var firstItems = firstJson.RootElement.GetProperty("items");
            Assert.Equal(2, firstItems.GetArrayLength());
            Assert.Equal("2026-10-01", firstItems[0].GetProperty("businessDate").GetString());
            Assert.Equal("2026-10-02", firstItems[1].GetProperty("businessDate").GetString());
            Assert.Equal("0.00", firstItems[1].GetProperty("balance").GetString());
            var cursor = firstJson.RootElement.GetProperty("nextCursor").GetString();
            Assert.False(string.IsNullOrWhiteSpace(cursor));
            using var second = await Get(client, $"/consolidations?from=2026-10-01&to=2026-10-04&pageSize=2&cursor={Uri.EscapeDataString(cursor)}");
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
            using var secondJson = JsonDocument.Parse(await second.Content.ReadAsStringAsync(Token));
            var secondItems = secondJson.RootElement.GetProperty("items");
            Assert.Equal("2026-10-03", secondItems[0].GetProperty("businessDate").GetString());
            Assert.Equal("-2.00", secondItems[0].GetProperty("balance").GetString());
            Assert.Equal("2026-10-04", secondItems[1].GetProperty("businessDate").GetString());
            Assert.Equal(JsonValueKind.Null, secondJson.RootElement.GetProperty("nextCursor").ValueKind);
            foreach (var path in new[]
            {
                "/consolidations?from=2026-10-01&to=2026-10-04&pageSize=2&cursor=bad",
                $"/consolidations?from=2026-10-01&to=2026-10-05&pageSize=2&cursor={Uri.EscapeDataString(cursor)}",
                $"/consolidations?from=2026-10-01&to=2026-10-04&pageSize=3&cursor={Uri.EscapeDataString(cursor)}",
                "/consolidations?from=2026-10-01&to=2027-10-03",
                "/consolidations?from=2026-10-01&to=2026-10-04&pageSize=201",
                "/consolidations/2026-1-1"
            })
            {
                using var invalid = await Get(client, path);
                await AssertProblem(invalid, HttpStatusCode.BadRequest);
            }
            var nextGeneration = Guid.NewGuid();
            await using (var insert = source.CreateCommand("INSERT INTO projection_generations (generation_id, status) VALUES ($1, 'ACTIVE')"))
            {
                insert.Parameters.AddWithValue(nextGeneration);
                await insert.ExecuteNonQueryAsync(Token);
            }
            await using (var update = source.CreateCommand("UPDATE projection_control SET active_generation_id = $1"))
            {
                update.Parameters.AddWithValue(nextGeneration);
                await update.ExecuteNonQueryAsync(Token);
            }
            using var conflict = await Get(client, $"/consolidations?from=2026-10-01&to=2026-10-04&pageSize=2&cursor={Uri.EscapeDataString(cursor)}");
            await AssertProblem(conflict, HttpStatusCode.Conflict);
            using var conflictJson = JsonDocument.Parse(await conflict.Content.ReadAsStringAsync(Token));
            Assert.Equal("projection-generation-changed", conflictJson.RootElement.GetProperty("type").GetString());
            Assert.Contains("Restart", conflictJson.RootElement.GetProperty("detail").GetString());
        });
    }

    [Fact]
    public async Task Jwt_permission_and_correlation_are_enforced_on_http()
    {
        await ProjectionTests.WithDatabase(async source =>
        {
            using var factory = ConsolidationApiTestHost.Create(FullConnectionString(source));
            using var client = factory.CreateClient();
            using (var noToken = await client.GetAsync("/consolidations/2026-10-22", Token))
                await AssertProblem(noToken, HttpStatusCode.Unauthorized);
            using (var invalid = await Get(client, "/consolidations/2026-10-22", "invalid"))
                await AssertProblem(invalid, HttpStatusCode.Unauthorized);
            using (var wrongIssuer = await Get(client, "/consolidations/2026-10-22",
                ConsolidationApiTestHost.Token(issuer: "https://other-issuer.example")))
                await AssertProblem(wrongIssuer, HttpStatusCode.Unauthorized);
            using (var wrongClient = await Get(client, "/consolidations/2026-10-22",
                ConsolidationApiTestHost.Token(clientId: "other-client")))
                await AssertProblem(wrongClient, HttpStatusCode.Unauthorized);
            using (var forbidden = await Get(client, "/consolidations/2026-10-22",
                ConsolidationApiTestHost.Token(permission: false)))
                await AssertProblem(forbidden, HttpStatusCode.Forbidden);
            using (var scoped = await Get(client, "/consolidations/2026-10-22",
                ConsolidationApiTestHost.Token(permission: false, scope: true)))
                Assert.Equal(HttpStatusCode.OK, scoped.StatusCode);
            var correlation = Guid.NewGuid();
            using var request = new HttpRequestMessage(HttpMethod.Get, "/consolidations/2026-10-22");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ConsolidationApiTestHost.Token());
            request.Headers.Add("X-Correlation-Id", correlation.ToString());
            using var success = await client.SendAsync(request, Token);
            Assert.Equal(HttpStatusCode.OK, success.StatusCode);
            Assert.Equal(correlation.ToString(), success.Headers.GetValues("X-Correlation-Id").Single());
        });
    }

    private static async Task<HttpResponseMessage> Get(HttpClient client, string path, string? token = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token ?? ConsolidationApiTestHost.Token());
        return await client.SendAsync(request, Token);
    }

    private static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.True(Guid.TryParse(response.Headers.GetValues("X-Correlation-Id").Single(), out _));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        Assert.Equal((int)status, json.RootElement.GetProperty("status").GetInt32());
        Assert.True(json.RootElement.TryGetProperty("type", out _));
        Assert.True(json.RootElement.TryGetProperty("title", out _));
        Assert.True(json.RootElement.TryGetProperty("correlationId", out _));
        Assert.True(json.RootElement.TryGetProperty("detail", out _));
    }

    private static async Task<Guid> ActiveGeneration(NpgsqlDataSource source)
    {
        await using var command = source.CreateCommand("SELECT active_generation_id FROM projection_control");
        return (Guid)(await command.ExecuteScalarAsync(Token))!;
    }

    private static string FullConnectionString(NpgsqlDataSource source) =>
        new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("CASHFLOW_TEST_CONSOLIDATION_POSTGRES"))
        {
            SearchPath = new NpgsqlConnectionStringBuilder(source.ConnectionString).SearchPath
        }.ConnectionString;

    private static async Task Seed(NpgsqlDataSource source, Guid generation, string date,
        decimal credits, decimal debits, long count)
    {
        await using var command = source.CreateCommand("""
            INSERT INTO daily_consolidations
                (generation_id, business_date, total_credits, total_debits, transaction_count)
            VALUES ($1, $2, $3, $4, $5)
            """);
        command.Parameters.AddWithValue(generation);
        command.Parameters.AddWithValue(DateOnly.Parse(date));
        command.Parameters.AddWithValue(credits);
        command.Parameters.AddWithValue(debits);
        command.Parameters.AddWithValue(count);
        await command.ExecuteNonQueryAsync(Token);
    }

    private static async Task Execute(NpgsqlDataSource source, string sql)
    {
        await using var command = source.CreateCommand(sql);
        await command.ExecuteNonQueryAsync(Token);
    }
}
