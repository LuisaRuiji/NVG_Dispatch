using System.Collections.Generic;
using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace NVGInventory.Tests;

public sealed class SqlServerWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly string _jwtSigningKey;
    private readonly IDictionary<string, string?> _originalEnvironment = new Dictionary<string, string?>();

    public SqlServerWebApplicationFactory(string connectionString, string jwtSigningKey)
    {
        _connectionString = connectionString;
        _jwtSigningKey = jwtSigningKey;
        JwtIssuer = "NVGInventory.Test";
        JwtAudience = "NVGInventory.Test";

        SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _connectionString);
        SetEnvironmentVariable("Jwt__Issuer", JwtIssuer);
        SetEnvironmentVariable("Jwt__Audience", JwtAudience);
        SetEnvironmentVariable("Jwt__Key", _jwtSigningKey);
        SetEnvironmentVariable("Jwt__AccessTokenMinutes", "60");
        SetEnvironmentVariable("Jwt__RefreshTokenDays", "7");
    }

    public string JwtIssuer { get; }
    public string JwtAudience { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
        });
        builder.ConfigureAppConfiguration((_, config) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["Jwt:Key"] = _jwtSigningKey,
                ["Jwt:Issuer"] = JwtIssuer,
                ["Jwt:Audience"] = JwtAudience,
                ["Jwt:AccessTokenMinutes"] = "60",
                ["Jwt:RefreshTokenDays"] = "7",
                ["FrontendBaseUrl"] = "http://localhost:5173",
                ["Cors:AllowedOrigins:0"] = "http://localhost:5173",
                ["ConnectionStrings:DefaultConnection"] = _connectionString,
                ["RateLimiting:Global:PermitLimit"] = "10000",
                ["RateLimiting:Global:WindowMinutes"] = "1",
                ["RateLimiting:Global:QueueLimit"] = "0",
                ["RateLimiting:Auth:PermitLimit"] = "10000",
                ["RateLimiting:Auth:WindowMinutes"] = "1",
                ["RateLimiting:Auth:QueueLimit"] = "0"
            };

            config.AddInMemoryCollection(settings);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        foreach (var kvp in _originalEnvironment)
        {
            Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
        }
    }

    private void SetEnvironmentVariable(string key, string value)
    {
        if (!_originalEnvironment.ContainsKey(key))
        {
            _originalEnvironment[key] = Environment.GetEnvironmentVariable(key);
        }

        Environment.SetEnvironmentVariable(key, value);
    }
}
