using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace CashFlow.EndToEndTests;

public sealed class ComposeTransactionFlowTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Keycloak_to_transactions_outbox_rabbitmq_and_consolidation_converges()
    {
        var secret = Environment.GetEnvironmentVariable("DEMO_CLIENT_SECRET");
        Assert.False(string.IsNullOrWhiteSpace(secret),
            "Set DEMO_CLIENT_SECRET to the cashflow-demo secret from the local Compose .env file.");

        var identityUrl = Environment.GetEnvironmentVariable("CASHFLOW_E2E_IDENTITY_URL") ?? "http://127.0.0.1:8080";
        var transactionsUrl = Environment.GetEnvironmentVariable("CASHFLOW_E2E_TRANSACTIONS_URL") ?? "http://127.0.0.1:8081";
        var consolidationUrl = Environment.GetEnvironmentVariable("CASHFLOW_E2E_CONSOLIDATION_URL") ?? "http://127.0.0.1:8082";
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        using var tokenResponse = await client.PostAsync(
            $"{identityUrl}/realms/cashflow/protocol/openid-connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = "cashflow-demo",
                ["client_secret"] = secret!
            }), Token);
        var tokenBody = await tokenResponse.Content.ReadAsStringAsync(Token);
        Assert.True(tokenResponse.IsSuccessStatusCode,
            $"Keycloak token request failed with HTTP {(int)tokenResponse.StatusCode}: {tokenBody}");
        using var tokenJson = JsonDocument.Parse(tokenBody);
        var accessToken = tokenJson.RootElement.GetProperty("access_token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(accessToken));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var date = await FindIsolatedDateAsync(client, transactionsUrl, consolidationUrl);
        var creditId = await CreateAsync(client, transactionsUrl, date, "CREDIT", "100.00");
        var debitId = await CreateAsync(client, transactionsUrl, date, "DEBIT", "20.00");
        Assert.NotEqual(creditId, debitId);

        var watch = Stopwatch.StartNew();
        var deadline = TimeSpan.FromSeconds(60);
        string lastObservation = "no consolidation response";
        while (watch.Elapsed < deadline)
        {
            using var response = await client.GetAsync($"{consolidationUrl}/consolidations/{date:yyyy-MM-dd}", Token);
            var body = await response.Content.ReadAsStringAsync(Token);
            lastObservation = $"HTTP {(int)response.StatusCode}: {body}";
            Assert.True(response.StatusCode == HttpStatusCode.OK, lastObservation);
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;
            Assert.Equal(date.ToString("yyyy-MM-dd"), root.GetProperty("businessDate").GetString());
            var count = root.GetProperty("transactionCount").GetInt64();
            if (count == 2)
            {
                Assert.Equal("100.00", root.GetProperty("totalCredits").GetString());
                Assert.Equal("20.00", root.GetProperty("totalDebits").GetString());
                Assert.Equal("80.00", root.GetProperty("balance").GetString());
                Assert.Equal("EVENTUAL", root.GetProperty("consistency").GetString());
                Assert.Equal("AVAILABLE", root.GetProperty("projectionState").GetString());
                Assert.Equal("UNKNOWN", root.GetProperty("completeness").GetString());
                return;
            }
            Assert.True(count is 0 or 1,
                $"Unexpected transaction count before convergence: {lastObservation}");
            await Task.Delay(TimeSpan.FromMilliseconds(250), Token);
        }
        Assert.Fail($"Consolidation did not converge within {deadline.TotalSeconds:F0}s for {date:yyyy-MM-dd}. Last observation: {lastObservation}");
    }

    private static async Task<DateOnly> FindIsolatedDateAsync(
        HttpClient client, string transactionsUrl, string consolidationUrl)
    {
        var first = new DateOnly(1900, 1, 1);
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var date = first.AddDays(RandomNumberGenerator.GetInt32(0, 36_525));
            var value = date.ToString("yyyy-MM-dd");
            using var transactions = await client.GetAsync(
                $"{transactionsUrl}/transactions?from={value}&to={value}", Token);
            var transactionBody = await transactions.Content.ReadAsStringAsync(Token);
            Assert.True(transactions.StatusCode == HttpStatusCode.OK,
                $"Could not check Transactions isolation for {value}: HTTP {(int)transactions.StatusCode}: {transactionBody}");
            using var transactionJson = JsonDocument.Parse(transactionBody);
            if (transactionJson.RootElement.GetProperty("items").GetArrayLength() != 0) continue;

            using var consolidation = await client.GetAsync(
                $"{consolidationUrl}/consolidations/{value}", Token);
            var consolidationBody = await consolidation.Content.ReadAsStringAsync(Token);
            Assert.True(consolidation.StatusCode == HttpStatusCode.OK,
                $"Could not check Consolidation isolation for {value}: HTTP {(int)consolidation.StatusCode}: {consolidationBody}");
            using var consolidationJson = JsonDocument.Parse(consolidationBody);
            if (consolidationJson.RootElement.GetProperty("transactionCount").GetInt64() == 0)
                return date;
        }
        throw new InvalidOperationException("Could not find an isolated business date after 20 candidates.");
    }

    private static async Task<Guid> CreateAsync(
        HttpClient client, string transactionsUrl, DateOnly date, string type, string amount)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{transactionsUrl}/transactions")
        {
            Content = JsonContent.Create(new
            {
                type,
                amount,
                occurredAt = $"{date:yyyy-MM-dd}T12:00:00-03:00",
                description = "Compose E2E"
            })
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Headers.Add("X-Correlation-Id", Guid.NewGuid().ToString());
        using var response = await client.SendAsync(request, Token);
        var body = await response.Content.ReadAsStringAsync(Token);
        Assert.True(response.StatusCode == HttpStatusCode.Created,
            $"Transactions POST {type} failed with HTTP {(int)response.StatusCode}: {body}");
        Assert.NotNull(response.Headers.Location);
        using var json = JsonDocument.Parse(body);
        Assert.Equal(date.ToString("yyyy-MM-dd"), json.RootElement.GetProperty("businessDate").GetString());
        Assert.Equal(type, json.RootElement.GetProperty("type").GetString());
        Assert.Equal(amount, json.RootElement.GetProperty("amount").GetString());
        return json.RootElement.GetProperty("id").GetGuid();
    }
}
