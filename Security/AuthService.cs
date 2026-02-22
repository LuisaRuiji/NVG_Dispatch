using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Entities;

namespace NVGInventory.Security;

public sealed record LoginCommand(string Username, string Password);

public sealed record AuthResult(
    Guid UserId,
    string Username,
    IReadOnlyCollection<string> Roles,
    string AccessToken,
    DateTime ExpiresAtUtc);

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

    public async Task<AuthResult?> TryLoginAsync(LoginCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Username) || string.IsNullOrWhiteSpace(command.Password))
        {
            return null;
        }

        var user = await _dbContext.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(
                u => u.Username == command.Username,
                cancellationToken);

        if (user is null || !user.IsActive)
        {
            return null;
        }

        var validPassword = BCrypt.Net.BCrypt.Verify(command.Password, user.PasswordHash);
        if (!validPassword)
        {
            return null;
        }

        var roles = user.UserRoles
            .Select(ur => ur.Role?.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .Distinct()
            .ToList();

        var token = _tokenService.CreateAccessToken(user, roles);

        return new AuthResult(
            user.Id,
            user.Username,
            roles,
            token.AccessToken,
            token.ExpiresAtUtc);
    }
}
