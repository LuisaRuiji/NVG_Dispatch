using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Entities;
using NVGInventory.Security;
using Xunit;

namespace NVGInventory.Tests;

public sealed class AuthRefreshTokenTests
{
    private const string SigningKey = "TESTING_ONLY_SIGNING_KEY_32_CHARS_MIN_123456";
    private const string Password = "plaintext";

    [Fact]
    public async Task TryLoginAsync_IssuesRefreshTokenAndStoresOnlyHash()
    {
        using var dbContext = CreateDbContext();
        await SeedUserAsync(dbContext);
        var service = CreateService(dbContext);

        var result = await service.TryLoginAsync(new LoginCommand("test_user", Password));

        Assert.True(result.Success);
        Assert.NotNull(result.Result);
        Assert.False(string.IsNullOrWhiteSpace(result.Result!.RefreshToken));

        var stored = await dbContext.RefreshTokens.SingleAsync();
        Assert.Equal(result.Result.UserId, stored.UserId);
        Assert.NotEqual(result.Result.RefreshToken, stored.TokenHash);
        Assert.Null(stored.RevokedAt);
    }

    [Fact]
    public async Task TryRefreshAsync_RotatesRefreshTokenAndRejectsReuse()
    {
        using var dbContext = CreateDbContext();
        await SeedUserAsync(dbContext);
        var service = CreateService(dbContext);
        var login = await service.TryLoginAsync(new LoginCommand("test_user", Password));
        var firstRefreshToken = login.Result!.RefreshToken;

        var refresh = await service.TryRefreshAsync(firstRefreshToken, "127.0.0.1", "test-agent");

        Assert.True(refresh.Success);
        Assert.NotNull(refresh.Result);
        Assert.NotEqual(firstRefreshToken, refresh.Result!.RefreshToken);

        var tokensAfterRefresh = await dbContext.RefreshTokens.ToListAsync();
        Assert.Equal(2, tokensAfterRefresh.Count);
        Assert.Contains(tokensAfterRefresh, token => token is { RevokedReason: "ROTATED", RevokedAt: not null });
        Assert.Single(tokensAfterRefresh, token => token.RevokedAt is null);

        var reuse = await service.TryRefreshAsync(firstRefreshToken, "127.0.0.1", "test-agent");

        Assert.False(reuse.Success);
        Assert.Null(reuse.Result);

        var activeTokens = await dbContext.RefreshTokens
            .Where(token => token.RevokedAt == null)
            .CountAsync();
        Assert.Equal(0, activeTokens);
    }

    [Fact]
    public async Task RevokeRefreshTokenAsync_CanRevokeByTokenWithoutAccessToken()
    {
        using var dbContext = CreateDbContext();
        await SeedUserAsync(dbContext);
        var service = CreateService(dbContext);
        var login = await service.TryLoginAsync(new LoginCommand("test_user", Password));
        var refreshToken = login.Result!.RefreshToken;

        await service.RevokeRefreshTokenAsync(refreshToken, "127.0.0.1");

        var stored = await dbContext.RefreshTokens.SingleAsync();
        Assert.NotNull(stored.RevokedAt);
        Assert.Equal("LOGOUT", stored.RevokedReason);

        var refresh = await service.TryRefreshAsync(refreshToken, "127.0.0.1", "test-agent");
        Assert.False(refresh.Success);
    }

    private static AuthService CreateService(InventoryDbContext dbContext)
    {
        var options = Options.Create(new JwtOptions
        {
            Issuer = "NVGInventory.Tests",
            Audience = "NVGInventory.Tests",
            Key = SigningKey,
            AccessTokenMinutes = 5,
            RefreshTokenDays = 7
        });

        return new AuthService(dbContext, new JwtTokenService(options), options);
    }

    private static async Task SeedUserAsync(InventoryDbContext dbContext)
    {
        var role = new Role { Id = 500, Name = RoleNames.InventoryOfficer };
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "test_user",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password)
        };

        dbContext.Roles.Add(role);
        dbContext.Users.Add(user);
        dbContext.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        await dbContext.SaveChangesAsync();
    }

    private static InventoryDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase($"auth-refresh-token-{Guid.NewGuid():N}")
            .Options;

        return new InventoryDbContext(options);
    }
}
