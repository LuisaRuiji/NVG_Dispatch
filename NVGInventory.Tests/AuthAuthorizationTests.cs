using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NVGInventory.Contracts;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Entities;
using NVGInventory.Domain.Enums;
using NVGInventory.Domain.Services;
using Xunit;

namespace NVGInventory.Tests;

[Collection("SqlServerIntegration")]
public sealed class AuthAuthorizationTests : IDisposable
{
    private const string TestSigningKey = "TESTING_ONLY_SIGNING_KEY_32_CHARS_MIN_123456";

    private readonly SqlServerIntegrationFixture _fixture;
    private readonly SqlServerWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthAuthorizationTests(SqlServerIntegrationFixture fixture)
    {
        _fixture = fixture;
        _factory = new SqlServerWebApplicationFactory(fixture.ConnectionString, TestSigningKey);
        _client = _factory.CreateClient(new()
        {
            BaseAddress = new Uri("https://localhost")
        });
    }
    
    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [SqlServerFact]
    public async Task Login_ReturnsValidJwt()
    {
        var username = $"io1_{Guid.NewGuid():N}";
        var password = "plaintext";
        var user = await CreateUserAsync(username, password, RoleNames.InventoryOfficer);

        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.AccessToken));
        Assert.True(string.IsNullOrWhiteSpace(payload.RefreshToken));
        Assert.Equal(user.Id, payload.UserId);
        Assert.Contains(RoleNames.InventoryOfficer, payload.Roles);
        AssertRefreshCookieHardened(GetRefreshCookie(response));

        var handler = new JwtSecurityTokenHandler();
        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = _factory.JwtIssuer,
            ValidAudience = _factory.JwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(TestSigningKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        handler.ValidateToken(payload.AccessToken, tokenValidationParameters, out _);
        var token = handler.ReadJwtToken(payload.AccessToken);

        Assert.Equal(user.Id.ToString(), token.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal(username, token.Claims.First(c => c.Type == JwtRegisteredClaimNames.UniqueName).Value);
        Assert.Contains(token.Claims, c => c.Type == "role" && c.Value == RoleNames.InventoryOfficer);
    }

    [SqlServerFact]
    public async Task Refresh_UsesHttpOnlyCookieAndRequiresCsrfHeader()
    {
        var username = $"io_refresh_{Guid.NewGuid():N}";
        var password = "plaintext";
        await CreateUserAsync(username, password, RoleNames.InventoryOfficer);

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var refreshCookie = GetRefreshCookie(loginResponse);
        AssertRefreshCookieHardened(refreshCookie);
        var cookieHeader = refreshCookie.Split(';', 2)[0];

        using var missingCsrfRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh")
        {
            Content = JsonContent.Create(new RefreshTokenRequest())
        };
        missingCsrfRequest.Headers.TryAddWithoutValidation("Cookie", cookieHeader);

        var missingCsrfResponse = await _client.SendAsync(missingCsrfRequest);
        Assert.Equal(HttpStatusCode.Forbidden, missingCsrfResponse.StatusCode);

        using var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh")
        {
            Content = JsonContent.Create(new RefreshTokenRequest())
        };
        refreshRequest.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
        refreshRequest.Headers.Add("X-NVG-CSRF", "1");

        var refreshResponse = await _client.SendAsync(refreshRequest);
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);

        var payload = await refreshResponse.Content.ReadFromJsonAsync<RefreshTokenResponse>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.AccessToken));
        Assert.True(string.IsNullOrWhiteSpace(payload.RefreshToken));
        AssertRefreshCookieHardened(GetRefreshCookie(refreshResponse));
    }

    [SqlServerFact]
    public async Task IoReview_RejectsMissingToken()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/requests/{Guid.NewGuid()}/io-review",
            new InventoryOfficerReviewRequest(
                null,
                null,
                null,
                new[] { new InventoryOfficerReviewLine(Guid.NewGuid(), 1m, null) }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SqlServerFact]
    public async Task IoReview_RejectsUserWithoutRole()
    {
        var token = await LoginAsAsync($"driver_{Guid.NewGuid():N}", "plaintext", RoleNames.Driver);

        var response = await PostWithTokenAsync(
            token,
            $"/api/requests/{Guid.NewGuid()}/io-review",
            new InventoryOfficerReviewRequest(
                null,
                null,
                null,
                new[] { new InventoryOfficerReviewLine(Guid.NewGuid(), 1m, null) }));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [SqlServerFact]
    public async Task Manager_CannotCallIoReview()
    {
        var token = await LoginAsAsync($"mgr_{Guid.NewGuid():N}", "plaintext", RoleNames.Manager);

        var response = await PostWithTokenAsync(
            token,
            $"/api/requests/{Guid.NewGuid()}/io-review",
            new InventoryOfficerReviewRequest(
                null,
                null,
                null,
                new[] { new InventoryOfficerReviewLine(Guid.NewGuid(), 1m, null) }));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [SqlServerFact]
    public async Task InventoryOfficer_CannotCallManagerDecision()
    {
        var token = await LoginAsAsync($"io_{Guid.NewGuid():N}", "plaintext", RoleNames.InventoryOfficer);

        var response = await PostWithTokenAsync(
            token,
            $"/api/requests/{Guid.NewGuid()}/manager-decision",
            new ManagerDecisionRequest(ApprovalDecision.Approve, null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [SqlServerFact]
    public async Task InventoryOfficer_CanReviewRequest()
    {
        var requester = await CreateUserAsync($"req_{Guid.NewGuid():N}", "plaintext");
        var ioUser = await CreateUserAsync($"io_{Guid.NewGuid():N}", "plaintext", RoleNames.InventoryOfficer);
        var (requestId, requestLineId) = await CreatePendingMaintenanceRequestAsync(requester.Id);

        var token = await LoginAsAsync(ioUser.Username, "plaintext");

        var response = await PostWithTokenAsync(
            token,
            $"/api/requests/{requestId}/io-review",
            new InventoryOfficerReviewRequest(
                null,
                null,
                null,
                new[] { new InventoryOfficerReviewLine(requestLineId, 1m, null) }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [SqlServerFact]
    public async Task IoReview_RejectsExpiredToken()
    {
        var expiredToken = CreateExpiredToken("expired_io", RoleNames.InventoryOfficer);

        var response = await PostWithTokenAsync(
            expiredToken,
            $"/api/requests/{Guid.NewGuid()}/io-review",
            new InventoryOfficerReviewRequest(
                null,
                null,
                null,
                new[] { new InventoryOfficerReviewLine(Guid.NewGuid(), 1m, null) }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<string> LoginAsAsync(string username, string password, params string[] roles)
    {
        await EnsureUserAsync(username, password, roles);

        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password));
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return payload!.AccessToken;
    }

    private async Task<User> CreateUserAsync(string username, string password, params string[] roles)
    {
        await using var context = _fixture.CreateDbContext();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            Email = null,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);

        context.Users.Add(user);

        if (roles.Length > 0)
        {
            var roleEntities = await context.Roles
                .Where(role => roles.Contains(role.Name))
                .ToListAsync();

            foreach (var role in roleEntities)
            {
                context.UserRoles.Add(new UserRole
                {
                    UserId = user.Id,
                    RoleId = role.Id
                });
            }
        }

        await context.SaveChangesAsync();
        return user;
    }

    private async Task EnsureUserAsync(string username, string password, params string[] roles)
    {
        await using var context = _fixture.CreateDbContext();
        var exists = await context.Users.AnyAsync(u => u.Username == username);
        if (exists)
        {
            return;
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            Email = null,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
        context.Users.Add(user);

        if (roles.Length > 0)
        {
            var roleEntities = await context.Roles
                .Where(role => roles.Contains(role.Name))
                .ToListAsync();

            foreach (var role in roleEntities)
            {
                context.UserRoles.Add(new UserRole
                {
                    UserId = user.Id,
                    RoleId = role.Id
                });
            }
        }

        await context.SaveChangesAsync();
    }

    private async Task<(Guid RequestId, Guid RequestLineId)> CreatePendingMaintenanceRequestAsync(Guid requesterUserId)
    {
        await using var context = _fixture.CreateDbContext();

        var asset = new Asset
        {
            Id = Guid.NewGuid(),
            AssetCode = $"TRK-{Guid.NewGuid():N}".Substring(0, 8),
            AssetType = AssetType.Truck
        };

        var item = new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = "Oil",
            Unit = "L",
            ItemType = ItemType.Consumable,
            Quantity = 10m
        };

        context.Assets.Add(asset);
        context.InventoryItems.Add(item);
        await context.SaveChangesAsync();

        var requestService = new RequestService(context);
        var approvalService = new ApprovalService(context, new UserService(context));
        var workflowService = new RequestWorkflowService(
            context,
            requestService,
            approvalService,
            new StockLedgerService(context),
            new UserService(context));

        var request = await requestService.CreateRequestAsync(
            new CreateRequestCommand(
                RequestType.MaintenanceIssue,
                requesterUserId,
                asset.Id,
                "Service",
                new[] { new RequestLineInput(item.Id, 1m, null) }));

        await workflowService.SubmitRequest(request.Id);

        var requestLineId = request.Lines.Single().Id;
        return (request.Id, requestLineId);
    }

    private async Task<HttpResponseMessage> PostWithTokenAsync<T>(string token, string url, T payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload)
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private string CreateExpiredToken(string username, string role)
    {
        var handler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(TestSigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow;

        var claims = new[]
        {
            new System.Security.Claims.Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new System.Security.Claims.Claim(JwtRegisteredClaimNames.UniqueName, username),
            new System.Security.Claims.Claim("role", role)
        };

        var token = new JwtSecurityToken(
            issuer: _factory.JwtIssuer,
            audience: _factory.JwtAudience,
            claims: claims,
            notBefore: now.AddMinutes(-10),
            expires: now.AddMinutes(-5),
            signingCredentials: creds);

        return handler.WriteToken(token);
    }

    private static string GetRefreshCookie(HttpResponseMessage response)
    {
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var setCookies));
        return Assert.Single(
            setCookies.Where(value => value.StartsWith("nvg_refresh_token=", StringComparison.OrdinalIgnoreCase)));
    }

    private static void AssertRefreshCookieHardened(string cookie)
    {
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/auth", cookie, StringComparison.OrdinalIgnoreCase);
    }
}
