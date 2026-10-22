using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Npgsql;
using Xunit;

namespace CashFlow.Transactions.IntegrationTests;

public sealed class TransactionIdempotencyApiTests
{
    private const string Command = """
        {"type":"CREDIT","amount":"100.00","occurredAt":"2026-10-21T12:00:00-03:00","description":"Venda"}
        """;

    [Fact]
    public async Task Requires_valid_token_issuer_client_and_write_permission()
    {
        using var factory = ApiTestHost.CreateFactory("Host=127.0.0.1;Port=1;Database=cashflow;Username=cashflow;Password=unused;Timeout=1");
        using var client = factory.CreateClient();
        var key = Guid.NewGuid();
        using var missing = await Post(client, key, Command, null);
        using var invalid = await Post(client, key, Command, "not-a-token");
        using var wrongIssuer = await Post(client, key, Command, ApiTestHost.Token(issuer: "https://wrong-issuer.example"));
        using var noPermission = await Post(client, key, Command, ApiTestHost.Token(canWrite: false));
        using var valid = await Post(client, key, Command, ApiTestHost.Token());
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongIssuer.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, noPermission.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, valid.StatusCode);
    }

    [Fact]
    public async Task Creates_replays_conflicts_and_scopes_keys_by_validated_client()
    {
        await WithDatabase(async (connection, source) =>
        {
            using var factory = ApiTestHost.CreateFactory(connection);
            using var client = factory.CreateClient();
            var key = Guid.NewGuid();
            using var first = await Post(client, key, Command, ApiTestHost.Token("client-a"));
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
            var firstBody = await first.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            using var firstJson = JsonDocument.Parse(firstBody);
            var id = firstJson.RootElement.GetProperty("id").GetGuid();
            Assert.Equal($"/transactions/{id}", first.Headers.Location?.ToString());

            using var replay = await Post(client, key, Command, ApiTestHost.Token("client-a"));
            Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
            Assert.Equal("true", replay.Headers.GetValues("Idempotent-Replayed").Single());
            Assert.Equal(first.Headers.Location, replay.Headers.Location);
            Assert.Equal(firstBody, await replay.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

            const string reordered = """
                { "description":"Venda", "occurredAt":"2026-10-21T15:00:00Z", "amount":"100.00", "type":"CREDIT" }
                """;
            using var semanticReplay = await Post(client, key, reordered, ApiTestHost.Token("client-a"));
            Assert.Equal(HttpStatusCode.Created, semanticReplay.StatusCode);
            Assert.Equal("true", semanticReplay.Headers.GetValues("Idempotent-Replayed").Single());
            Assert.Equal(firstBody, await semanticReplay.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

            using var conflict = await Post(client, key, Command.Replace("100.00", "101.00"), ApiTestHost.Token("client-a"));
            Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
            Assert.Equal("application/problem+json", conflict.Content.Headers.ContentType?.MediaType);
            Assert.Equal((1L, 1L, 1L), await Counts(source));

            using var otherKey = await Post(client, Guid.NewGuid(), Command, ApiTestHost.Token("client-a"));
            using var otherClient = await Post(client, key, Command, ApiTestHost.Token("client-b"));
            Assert.Equal(HttpStatusCode.Created, otherKey.StatusCode);
            Assert.Equal(HttpStatusCode.Created, otherClient.StatusCode);
            Assert.Equal((3L, 3L, 3L), await Counts(source));

            await using var inspect = source.CreateCommand("""
                SELECT f.creation_event_id, o.event_id, f.business_date, o.payload->>'businessDate',
                       f.actor_issuer, f.actor_client_id, o.state, o.payload->>'transactionId'
                FROM financial_transactions f JOIN outbox_messages o ON o.aggregate_id = f.id WHERE f.id = $1
                """);
            inspect.Parameters.AddWithValue(id);
            await using var reader = await inspect.ExecuteReaderAsync(TestContext.Current.CancellationToken);
            Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
            Assert.Equal(reader.GetGuid(0), reader.GetGuid(1));
            Assert.Equal(reader.GetFieldValue<DateOnly>(2).ToString("yyyy-MM-dd"), reader.GetString(3));
            Assert.Equal("https://test-issuer.example", reader.GetString(4));
            Assert.Equal("client-a", reader.GetString(5));
            Assert.Equal("PENDING", reader.GetString(6));
            Assert.Equal(id.ToString(), reader.GetString(7));
        });
    }

    [Fact]
    public async Task Concurrent_requests_create_one_fact_key_and_event()
    {
        await WithDatabase(async (connection, source) =>
        {
            using var factory = ApiTestHost.CreateFactory(connection);
            using var client = factory.CreateClient();
            var key = Guid.NewGuid();
            var requests = Enumerable.Range(0, 8).Select(_ => Post(client, key, Command, ApiTestHost.Token())).ToArray();
            var responses = await Task.WhenAll(requests);
            try
            {
                Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
                var ids = new List<Guid>();
                foreach (var response in responses)
                {
                    using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
                    ids.Add(json.RootElement.GetProperty("id").GetGuid());
                }
                Assert.Single(ids.Distinct());
                Assert.Equal(7, responses.Count(response => response.Headers.Contains("Idempotent-Replayed")));
                Assert.Equal((1L, 1L, 1L), await Counts(source));
            }
            finally { foreach (var response in responses) response.Dispose(); }
        });
    }

    [Fact]
    public async Task Outbox_failure_rolls_back_all_three_rows()
    {
        await WithDatabase(async (connection, source) =>
        {
            await using (var trigger = source.CreateCommand("""
                CREATE FUNCTION reject_outbox() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'rollback test'; END; $$;
                CREATE TRIGGER reject_outbox BEFORE INSERT ON outbox_messages
                FOR EACH ROW EXECUTE FUNCTION reject_outbox();
                """)) await trigger.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            using var factory = ApiTestHost.CreateFactory(connection);
            using var client = factory.CreateClient();
            using var response = await Post(client, Guid.NewGuid(), Command, ApiTestHost.Token());
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Equal((0L, 0L, 0L), await Counts(source));
        });
    }

    private static async Task<HttpResponseMessage> Post(HttpClient client, Guid key, string body, string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/transactions");
        request.Headers.Add("Idempotency-Key", key.ToString());
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<(long, long, long)> Counts(NpgsqlDataSource source)
    {
        await using var command = source.CreateCommand("""
            SELECT (SELECT count(*) FROM financial_transactions),
                   (SELECT count(*) FROM idempotency_records),
                   (SELECT count(*) FROM outbox_messages)
            """);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        return (reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2));
    }

    private static async Task WithDatabase(Func<string, NpgsqlDataSource, Task> test)
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
            var scopedConnection = new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = schema }.ConnectionString;
            await using var source = NpgsqlDataSource.Create(scopedConnection);
            foreach (var migration in new[] { "001_financial_transactions.sql", "002_idempotency_outbox.sql" })
            {
                await using var command = source.CreateCommand(await File.ReadAllTextAsync(
                    Path.Combine(AppContext.BaseDirectory, migration), TestContext.Current.CancellationToken));
                await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            }
            await test(scopedConnection, source);
        }
        finally
        {
            await using var admin = NpgsqlDataSource.Create(connectionString);
            await using var drop = admin.CreateCommand($"DROP SCHEMA {schema} CASCADE");
            await drop.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }
    }
}
