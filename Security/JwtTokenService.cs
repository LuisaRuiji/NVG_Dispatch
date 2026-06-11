using System.IdentityModel.Tokens.Jwt;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NVGInventory.Domain.Entities;

namespace NVGInventory.Security;

public sealed record TokenResult(string AccessToken, DateTime ExpiresAtUtc, string Jti);

public sealed class JwtTokenService
{
    private readonly JwtOptions _options;
    private readonly SigningCredentials _signingCredentials;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
        if (string.IsNullOrWhiteSpace(_options.Key))
        {
            throw new InvalidOperationException("JWT signing key is not configured.");
        }

        var keyBytes = Encoding.UTF8.GetBytes(_options.Key);
        if (keyBytes.Length < 32)
        {
            throw new InvalidOperationException("JWT signing key must be at least 32 bytes.");
        }

        _signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(keyBytes),
            SecurityAlgorithms.HmacSha256);
    }

    public TokenResult CreateAccessToken(
        User user,
        IReadOnlyCollection<string> roles,
        DateTime? mfaPerformedAtUtc = null)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_options.AccessTokenMinutes);

        var jti = Guid.NewGuid().ToString();
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(JwtRegisteredClaimNames.Jti, jti),
            new("amr", "pwd")
        };

        if (mfaPerformedAtUtc.HasValue)
        {
            var mfaAt = new DateTimeOffset(DateTime.SpecifyKind(mfaPerformedAtUtc.Value, DateTimeKind.Utc))
                .ToUnixTimeSeconds()
                .ToString(CultureInfo.InvariantCulture);
            claims.Add(new Claim("amr", "mfa"));
            claims.Add(new Claim("mfa_at", mfaAt));
        }

        foreach (var role in roles)
        {
            claims.Add(new Claim("role", role));
        }

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: _signingCredentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        return new TokenResult(tokenString, expires, jti);
    }
}
