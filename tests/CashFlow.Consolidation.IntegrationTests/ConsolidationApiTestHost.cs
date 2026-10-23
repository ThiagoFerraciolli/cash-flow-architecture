using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace CashFlow.Consolidation.IntegrationTests;

internal static class ConsolidationApiTestHost
{
    private const string Issuer = "https://test-issuer.example";
    private const string Audience = "cashflow-api";
    private static readonly object Sync = new();
    private static readonly RSA SigningKey = RSA.Create(2048);

    public static WebApplicationFactory<Program> Create(string connectionString)
    {
        lock (Sync)
        {
            var values = new Dictionary<string, string>
            {
                ["ConnectionStrings__Consolidation"] = connectionString,
                ["Authentication__Authority"] = Issuer,
                ["Authentication__Issuer"] = Issuer,
                ["Authentication__Audience"] = Audience,
                ["Authentication__AllowedClientIds"] = "client-a"
            };
            var previous = values.Keys.ToDictionary(key => key, Environment.GetEnvironmentVariable);
            try
            {
                foreach (var (key, value) in values) Environment.SetEnvironmentVariable(key, value);
                var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
                    builder.ConfigureTestServices(services => services.PostConfigure<JwtBearerOptions>(
                        JwtBearerDefaults.AuthenticationScheme, options =>
                        {
                            options.Authority = null;
                            options.MetadataAddress = string.Empty;
                            options.ConfigurationManager = null;
                            options.TokenValidationParameters.IssuerSigningKey = new RsaSecurityKey(SigningKey);
                        })));
                _ = factory.Server;
                return factory;
            }
            finally
            {
                foreach (var (key, value) in previous) Environment.SetEnvironmentVariable(key, value);
            }
        }
    }

    public static string Token(bool permission = true, string? issuer = null, string clientId = "client-a",
        bool scope = false)
    {
        var claims = new List<Claim> { new("client_id", clientId) };
        if (permission) claims.Add(new Claim("permissions", "consolidations.read"));
        if (scope) claims.Add(new Claim("scope", "cashflow/consolidations.read"));
        var jwt = new JwtSecurityToken(issuer ?? Issuer, Audience, claims,
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(10),
            new SigningCredentials(new RsaSecurityKey(SigningKey), SecurityAlgorithms.RsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }
}
