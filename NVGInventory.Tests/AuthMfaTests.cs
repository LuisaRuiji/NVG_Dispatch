using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Entities;
using NVGInventory.Security;
using Xunit;

namespace NVGInventory.Tests;

public sealed class AuthMfaTests
{
    private const string SigningKey = "TESTING_ONLY_SIGNING_KEY_32_CHARS_MIN_123456";
    private const string Password = "plaintext";
    private const string MfaSecret = "JBSWY3DPEHPK3PXP";

    [Fact]
    public async Task TryLoginAsync_WhenMfaEnabled_ReturnsChallengeWithoutTokens()
    {
        using var dbContext = CreateDbContext();
        await SeedUserAsync(dbContext, mfaEnabled: true);
        var service = CreateService(dbContext);

        var result = await service.TryLoginAsync(new LoginCommand("test_user", Password));

        Assert.False(result.Success);
        Assert.True(result.RequiresMfa);
        Assert.NotNull(result.MfaChallenge);
        Assert.Null(result.Result);
        Assert.Equal(AuthFailureReasons.MfaRequired, result.FailureReason);
        Assert.Empty(await dbContext.RefreshTokens.ToListAsync());
        Assert.Single(await dbContext.MfaChallenges.ToListAsync());
    }

    [Fact]
    public async Task TryVerifyMfaLoginAsync_WithValidCode_IssuesMfaTokenAndConsumesChallenge()
    {
        using var dbContext = CreateDbContext();
        await SeedUserAsync(dbContext, mfaEnabled: true);
        var service = CreateService(dbContext);
        var login = await service.TryLoginAsync(new LoginCommand("test_user", Password));
        var code = GenerateTotp(MfaSecret);

        var result = await service.TryVerifyMfaLoginAsync(
            login.MfaChallenge!.ChallengeId,
            code,
            "127.0.0.1",
            "test-agent");

        Assert.True(result.Success);
        Assert.NotNull(result.Result);
        Assert.False(string.IsNullOrWhiteSpace(result.Result!.RefreshToken));
        Assert.Single(await dbContext.RefreshTokens.ToListAsync());

        var challenge = await dbContext.MfaChallenges.SingleAsync();
        Assert.NotNull(challenge.ConsumedAt);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Result.AccessToken);
        Assert.Contains(jwt.Claims, claim => claim.Type == "amr" && claim.Value == "mfa");
        Assert.Contains(jwt.Claims, claim => claim.Type == "mfa_at");
    }

    [Fact]
    public async Task TryStepUpAsync_WithValidCode_IssuesRecentMfaTokenWithoutRefreshToken()
    {
        using var dbContext = CreateDbContext();
        var user = await SeedUserAsync(dbContext, mfaEnabled: true);
        var service = CreateService(dbContext);
        var code = GenerateTotp(MfaSecret);

        var result = await service.TryStepUpAsync(user.Id, code);

        Assert.True(result.Success);
        Assert.NotNull(result.Result);
        Assert.Empty(await dbContext.RefreshTokens.ToListAsync());

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Result!.AccessToken);
        Assert.Contains(jwt.Claims, claim => claim.Type == "amr" && claim.Value == "mfa");
        Assert.Contains(jwt.Claims, claim => claim.Type == "mfa_at");
    }

    private static AuthService CreateService(InventoryDbContext dbContext)
    {
        var jwtOptions = Options.Create(new JwtOptions
        {
            Issuer = "NVGInventory.Tests",
            Audience = "NVGInventory.Tests",
            Key = SigningKey,
            AccessTokenMinutes = 5,
            RefreshTokenDays = 7
        });

        return new AuthService(
            dbContext,
            new JwtTokenService(jwtOptions),
            jwtOptions,
            new TotpAuthenticator(),
            Options.Create(new MfaOptions()));
    }

    private static async Task<User> SeedUserAsync(InventoryDbContext dbContext, bool mfaEnabled)
    {
        var role = new Role { Id = 500, Name = RoleNames.InventoryOfficer };
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "test_user",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
            MfaEnabled = mfaEnabled,
            MfaSecretKey = mfaEnabled ? MfaSecret : null,
            MfaEnabledAt = mfaEnabled ? DateTime.UtcNow : null
        };

        dbContext.Roles.Add(role);
        dbContext.Users.Add(user);
        dbContext.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        await dbContext.SaveChangesAsync();
        return user;
    }

    private static InventoryDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase($"auth-mfa-{Guid.NewGuid():N}")
            .Options;

        return new InventoryDbContext(options);
    }

    private static string GenerateTotp(string secret)
    {
        var secretBytes = DecodeBase32(secret);
        var timeStep = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        var counter = BitConverter.GetBytes(timeStep);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(counter);
        }

        using var hmac = new HMACSHA1(secretBytes);
        var hash = hmac.ComputeHash(counter);
        var offset = hash[^1] & 0x0f;
        var binary =
            ((hash[offset] & 0x7f) << 24)
            | ((hash[offset + 1] & 0xff) << 16)
            | ((hash[offset + 2] & 0xff) << 8)
            | (hash[offset + 3] & 0xff);

        return (binary % 1_000_000).ToString("D6");
    }

    private static byte[] DecodeBase32(string value)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bytes = new List<byte>();
        var buffer = 0;
        var bitsLeft = 0;

        foreach (var character in value.TrimEnd('=').ToUpperInvariant())
        {
            var index = alphabet.IndexOf(character, StringComparison.Ordinal);
            buffer = (buffer << 5) | index;
            bitsLeft += 5;

            if (bitsLeft >= 8)
            {
                bytes.Add((byte)((buffer >> (bitsLeft - 8)) & 0xff));
                bitsLeft -= 8;
            }
        }

        return bytes.ToArray();
    }
}
