using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Entities;
using NVGInventory.Domain.Exceptions;

namespace NVGInventory.Domain.Services;

public sealed record CreateUserCommand(string Username, string Password, string? Email);

public sealed class UserService
{
    private readonly InventoryDbContext _dbContext;

    public UserService(InventoryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<User> CreateUserAsync(CreateUserCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Username))
        {
            throw new BusinessRuleViolationException("Username is required.");
        }

        if (string.IsNullOrWhiteSpace(command.Password))
        {
            throw new BusinessRuleViolationException("Password is required.");
        }

        PasswordPolicy.EnsureValid(command.Password);

        var username = command.Username.Trim();
        var exists = await _dbContext.Users
            .AnyAsync(user => user.Username == username, cancellationToken);

        if (exists)
        {
            throw new BusinessRuleViolationException("Username already exists.");
        }

        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            Email = command.Email,
            IsActive = true,
            CreatedAt = now
        };

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(command.Password);

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return user;
    }

    public async Task AssignRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roleName))
        {
            throw new BusinessRuleViolationException("Role name is required.");
        }

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new NotFoundException("User not found.");
        }

        var role = await _dbContext.Roles
            .FirstOrDefaultAsync(r => r.Name == roleName, cancellationToken);

        if (role is null)
        {
            throw new NotFoundException("Role not found.");
        }

        var exists = await _dbContext.UserRoles
            .AnyAsync(ur => ur.UserId == userId && ur.RoleId == role.Id, cancellationToken);

        if (!exists)
        {
            _dbContext.UserRoles.Add(new UserRole
            {
                UserId = userId,
                RoleId = role.Id
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task EnsureUserHasRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new NotFoundException("User not found.");
        }

        if (!user.IsActive)
        {
            throw new BusinessRuleViolationException("User must be active.");
        }

        var hasRole = user.UserRoles.Any(ur => string.Equals(ur.Role?.Name, roleName, StringComparison.OrdinalIgnoreCase));
        if (!hasRole)
        {
            throw new BusinessRuleViolationException($"User must have role {roleName}.");
        }
    }

    public async Task EnsureActiveUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new NotFoundException("User not found.");
        }

        if (!user.IsActive)
        {
            throw new BusinessRuleViolationException("User must be active.");
        }
    }

    public async Task<IReadOnlyCollection<User>> GetUsersWithRolesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .OrderBy(u => u.Username)
            .ToListAsync(cancellationToken);
    }

    public async Task<User?> GetUserWithRolesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
    }

    public async Task ReplaceRolesAsync(Guid userId, IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new NotFoundException("User not found.");
        }

        var roles = await _dbContext.Roles
            .Where(r => roleNames.Contains(r.Name))
            .ToListAsync(cancellationToken);

        if (roles.Count != roleNames.Count)
        {
            throw new NotFoundException("One or more roles not found.");
        }

        _dbContext.UserRoles.RemoveRange(user.UserRoles);

        foreach (var role in roles)
        {
            _dbContext.UserRoles.Add(new UserRole
            {
                UserId = userId,
                RoleId = role.Id
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateUserStatusAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new NotFoundException("User not found.");
        }

        user.IsActive = isActive;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ResetPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newPassword))
        {
            throw new BusinessRuleViolationException("Password is required.");
        }

        PasswordPolicy.EnsureValid(newPassword);

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new NotFoundException("User not found.");
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeactivateUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new NotFoundException("User not found.");
        }

        user.IsActive = false;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<string>> GetRoleNamesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Roles
            .Select(r => r.Name)
            .OrderBy(name => name)
            .ToListAsync(cancellationToken);
    }

}
