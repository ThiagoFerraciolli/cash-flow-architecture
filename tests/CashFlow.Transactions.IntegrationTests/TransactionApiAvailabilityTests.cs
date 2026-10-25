using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Npgsql;
using Xunit;

namespace CashFlow.Transactions.IntegrationTests;

public sealed class TransactionApiAvailabilityTests
{
    [Fact]
    public async Task Unavailable_postgres_returns_problem_503_for_database_routes()
    {
        using var factory = ApiTestHost.CreateFactory("Host=127.0.0.1;Port=1;Database=cashflow;Username=cashflow;Password=unused;Timeout=1;Command Timeout=1");
        using var client = factory.CreateClient();
        using var live = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);
        using var ready = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);

        using var post = await SendPost(client);
        using var byId = await SendGet(client, $"/transactions/{Guid.NewGuid()}");
        using var list = await SendGet(client, "/transactions?from=2026-10-21&to=2026-10-21");
        foreach (var response in new[] { post, byId, list })
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            Assert.True(Guid.TryParse(response.Headers.GetValues("X-Correlation-Id").Single(), out _));
            Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
            using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            Assert.Equal(503, problem.RootElement.GetProperty("status").GetInt32());
        }
    }

    [Fact]
    public async Task Transaction_reads_require_authentication_and_read_permission()
    {
        using var factory = ApiTestHost.CreateFactory("Host=127.0.0.1;Port=1;Database=cashflow;Username=cashflow;Password=unused;Timeout=1;Command Timeout=1");
        using var client = factory.CreateClient();

        using var anonymous = await client.GetAsync($"/transactions/{Guid.NewGuid()}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        using var writeOnlyRequest = new HttpRequestMessage(HttpMethod.Get, $"/transactions/{Guid.NewGuid()}");
        writeOnlyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiTestHost.Token(canWrite: true, canRead: false));
        using var writeOnly = await client.SendAsync(writeOnlyRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, writeOnly.StatusCode);
    }

    [Fact]
    public async Task Available_postgres_preserves_create_get_and_list()
    {
        var connectionString = Environment.GetEnvironmentVariable("CASHFLOW_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) Assert.Skip("Set CASHFLOW_TEST_POSTGRES to a dedicated PostgreSQL test database.");
        var schema = "test_" + Guid.NewGuid().ToString("N");
        await using (var admin = NpgsqlDataSource.Create(connectionString))
        {
            await using var create = admin.CreateCommand($"CREATE SCHEMA {schema}");
            await create.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }
        try
        {
            var testConnection = new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = schema }.ConnectionString;
            await using (var database = NpgsqlDataSource.Create(testConnection))
            {
                await using var migration = database.CreateCommand(await File.ReadAllTextAsync(
                    Path.Combine(AppContext.BaseDirectory, "001_financial_transactions.sql"), TestContext.Current.CancellationToken));
                await migration.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
                await using var migration2 = database.CreateCommand(await File.ReadAllTextAsync(
                    Path.Combine(AppContext.BaseDirectory, "002_idempotency_outbox.sql"), TestContext.Current.CancellationToken));
                await migration2.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            }
            using var factory = ApiTestHost.CreateFactory(testConnection);
            using var client = factory.CreateClient();
            using var ready = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
            using var post = await SendPost(client);
            Assert.Equal(HttpStatusCode.Created, post.StatusCode);
            using var created = JsonDocument.Parse(await post.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            var id = created.RootElement.GetProperty("id").GetGuid();
            using var byId = await SendGet(client, $"/transactions/{id}");
            Assert.Equal(HttpStatusCode.OK, byId.StatusCode);
            using var list = await SendGet(client, "/transactions?from=2026-10-21&to=2026-10-21");
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            using var items = JsonDocument.Parse(await list.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            Assert.Equal(id, items.RootElement.GetProperty("items")[0].GetProperty("id").GetGuid());
        }
        finally
        {
            await using var admin = NpgsqlDataSource.Create(connectionString);
            await using var drop = admin.CreateCommand($"DROP SCHEMA {schema} CASCADE");
            await drop.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }
    }

    private static async Task<HttpResponseMessage> SendPost(HttpClient client)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/transactions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiTestHost.Token());
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = new StringContent("""
            {"type":"CREDIT","amount":"100.00","occurredAt":"2026-10-21T12:00:00-03:00","description":"availability test"}
            """, Encoding.UTF8, "application/json");
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<HttpResponseMessage> SendGet(HttpClient client, string uri)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiTestHost.Token());
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
