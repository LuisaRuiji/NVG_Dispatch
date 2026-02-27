using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Entities;

namespace NVGInventory.Security;

public sealed record LoginCommand(string Username, string Password);

public sealed record AuthResult(
    Guid UserId,
    string Username,
    IReadOnlyCollection<string> Roles,
    string AccessToken,
    DateTime ExpiresAtUtc,
    string TokenJti);

public sealed record AuthAttemptResult(
    bool Success,
    AuthResult? Result,
    string? FailureReason,
    Guid? UserId,
    string? Username,
    IReadOnlyCollection<string> RolesSnapshot);

public sealed class AuthService
{
    private readonly InventoryDbContext _dbContext;
    private readonly JwtTokenService _tokenService;

    public AuthService(
        InventoryDbContext dbContext,
        JwtTokenService tokenService)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
    }

    public async Task<AuthAttemptResult> TryLoginAsync(LoginCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Username) || string.IsNullOrWhiteSpace(command.Password))
        {
            return new AuthAttemptResult(false, null, AuthFailureReasons.ValidationError, null, null, Array.Empty<string>());
        }

        var user = await _dbContext.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(
                u => u.Username == command.Username,
                cancellationToken);

        if (user is null)
        {
            return new AuthAttemptResult(false, null, AuthFailureReasons.UserNotFound, null, command.Username.Trim(), Array.Empty<string>());
        }

        if (!user.IsActive)
        {
            return new AuthAttemptResult(
                false,
                null,
                AuthFailureReasons.UserInactive,
                user.Id,
                user.Username,
                user.UserRoles
                    .Select(ur => ur.Role?.Name)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Select(name => name!)
                    .Distinct()
                    .ToList());
        }

        var validPassword = BCrypt.Net.BCrypt.Verify(command.Password, user.PasswordHash);
        if (!validPassword)
        {
            return new AuthAttemptResult(
                false,
                null,
                AuthFailureReasons.InvalidCredentials,
                user.Id,
                user.Username,
                user.UserRoles
                    .Select(ur => ur.Role?.Name)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Select(name => name!)
                    .Distinct()
                    .ToList());
        }

        var roles = user.UserRoles
            .Select(ur => ur.Role?.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .Distinct()
            .ToList();

        var token = _tokenService.CreateAccessToken(user, roles);

        var result = new AuthResult(
            user.Id,
            user.Username,
            roles,
            token.AccessToken,
            token.ExpiresAtUtc,
            token.Jti);

        return new AuthAttemptResult(
            true,
            result,
            null,
            result.UserId,
            result.Username,
            result.Roles);
    }
}
