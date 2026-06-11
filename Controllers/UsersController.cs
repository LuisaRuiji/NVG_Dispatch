using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NVGInventory.Contracts;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Entities;
using NVGInventory.Domain.Services;
using NVGInventory.Security;

namespace NVGInventory.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public sealed class UsersController : ControllerBase
{
    private readonly UserService _userService;
    private readonly IAuditService _auditService;
    private readonly InventoryDbContext _dbContext;

    public UsersController(
        UserService userService,
        IAuditService auditService,
        InventoryDbContext dbContext)
    {
        _userService = userService;
        _auditService = auditService;
        _dbContext = dbContext;
    }

    [HttpGet]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.SuperAdmin}")]
    public async Task<ActionResult<IReadOnlyCollection<UserSummaryResponse>>> GetUsers(CancellationToken cancellationToken)
    {
        var isSuperAdmin = User.IsInRole(RoleNames.SuperAdmin);
        var users = await _userService.GetUsersWithRolesAsync(cancellationToken);

        if (!isSuperAdmin)
        {
            users = users
                .Where(user => user.UserRoles.All(ur =>
                    ur.Role?.Name != RoleNames.SuperAdmin && ur.Role?.Name != RoleNames.Admin))
                .ToList();
        }

        var response = users
            .Select(user => new UserSummaryResponse(
                user.Id,
                user.Username,
                user.Email,
                user.IsActive,
                user.CreatedAt,
                user.UserRoles.Select(ur => ur.Role?.Name ?? string.Empty)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .ToList()))
            .ToList();

        return Ok(response);
    }

    [HttpGet("roles")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.SuperAdmin}")]
    public async Task<ActionResult<IReadOnlyCollection<RoleSummaryResponse>>> GetRoles(CancellationToken cancellationToken)
    {
        var isSuperAdmin = User.IsInRole(RoleNames.SuperAdmin);
        var roleNames = (await _userService.GetRoleNamesAsync(cancellationToken)).ToList();

        if (!isSuperAdmin)
        {
            roleNames = roleNames
                .Where(name => name != RoleNames.Admin && name != RoleNames.SuperAdmin)
                .ToList();
        }

        return Ok(roleNames.Select(name => new RoleSummaryResponse(name)).ToList());
    }

    [HttpPost]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.SuperAdmin}")]
    [Authorize(Policy = AuthorizationPolicies.RequireRecentMfa)]
    public async Task<ActionResult<CreateUserResponse>> CreateUser(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await _userService.CreateUserAsync(
            new CreateUserCommand(request.Username, request.Password, request.Email),
            cancellationToken);

        await AddUserAuditAsync(
            AuditActions.UserCreated,
            user.Id,
            before: null,
            after: new { user.Username, user.Email, user.IsActive },
            cancellationToken);

        return Ok(new CreateUserResponse(user.Id, user.Username));
    }

    [HttpPost("{userId:guid}/roles")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.SuperAdmin}")]
    [Authorize(Policy = AuthorizationPolicies.RequireRecentMfa)]
    public async Task<IActionResult> AssignRole(Guid userId, AssignRoleRequest request, CancellationToken cancellationToken)
    {
        var isSuperAdmin = User.IsInRole(RoleNames.SuperAdmin);
        var target = await _userService.GetUserWithRolesAsync(userId, cancellationToken);

        if (target is null)
        {
            return NotFound();
        }

        var targetHasAdmin = target.UserRoles.Any(ur => ur.Role?.Name == RoleNames.Admin);
        var targetHasSuper = target.UserRoles.Any(ur => ur.Role?.Name == RoleNames.SuperAdmin);

        if (!isSuperAdmin && (targetHasAdmin || targetHasSuper))
        {
            return Forbid();
        }

        if (!isSuperAdmin && (request.RoleName == RoleNames.Admin || request.RoleName == RoleNames.SuperAdmin))
        {
            return Forbid();
        }

        var beforeRoles = GetRoleNames(target);
        await _userService.AssignRoleAsync(userId, request.RoleName, cancellationToken);
        var afterRoles = beforeRoles
            .Append(request.RoleName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name)
            .ToList();
        await AddUserAuditAsync(
            AuditActions.UserRoleAssigned,
            userId,
            new { Roles = beforeRoles },
            new { Roles = afterRoles, AssignedRole = request.RoleName },
            cancellationToken);

        return NoContent();
    }

    [HttpPut("{userId:guid}/roles")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.SuperAdmin}")]
    [Authorize(Policy = AuthorizationPolicies.RequireRecentMfa)]
    public async Task<IActionResult> ReplaceRoles(Guid userId, UpdateUserRolesRequest request, CancellationToken cancellationToken)
    {
        var isSuperAdmin = User.IsInRole(RoleNames.SuperAdmin);
        var target = await _userService.GetUserWithRolesAsync(userId, cancellationToken);

        if (target is null)
        {
            return NotFound();
        }

        var targetHasAdmin = target.UserRoles.Any(ur => ur.Role?.Name == RoleNames.Admin);
        var targetHasSuper = target.UserRoles.Any(ur => ur.Role?.Name == RoleNames.SuperAdmin);

        if (!isSuperAdmin && (targetHasAdmin || targetHasSuper))
        {
            return Forbid();
        }

        if (!isSuperAdmin && request.RoleNames.Any(name => name == RoleNames.Admin || name == RoleNames.SuperAdmin))
        {
            return Forbid();
        }

        var beforeRoles = GetRoleNames(target);
        await _userService.ReplaceRolesAsync(userId, request.RoleNames, cancellationToken);
        await AddUserAuditAsync(
            AuditActions.UserRolesReplaced,
            userId,
            new { Roles = beforeRoles },
            new { Roles = NormalizeRoleNames(request.RoleNames) },
            cancellationToken);

        return NoContent();
    }

    [HttpPatch("{userId:guid}/status")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.SuperAdmin}")]
    [Authorize(Policy = AuthorizationPolicies.RequireRecentMfa)]
    public async Task<IActionResult> UpdateStatus(Guid userId, UpdateUserStatusRequest request, CancellationToken cancellationToken)
    {
        var isSuperAdmin = User.IsInRole(RoleNames.SuperAdmin);
        var target = await _userService.GetUserWithRolesAsync(userId, cancellationToken);

        if (target is null)
        {
            return NotFound();
        }

        var targetHasAdmin = target.UserRoles.Any(ur => ur.Role?.Name == RoleNames.Admin);
        var targetHasSuper = target.UserRoles.Any(ur => ur.Role?.Name == RoleNames.SuperAdmin);

        if (!isSuperAdmin && (targetHasAdmin || targetHasSuper))
        {
            return Forbid();
        }

        var wasActive = target.IsActive;
        await _userService.UpdateUserStatusAsync(userId, request.IsActive, cancellationToken);
        await AddUserAuditAsync(
            AuditActions.UserStatusUpdated,
            userId,
            new { IsActive = wasActive },
            new { request.IsActive },
            cancellationToken);

        return NoContent();
    }

    [HttpPost("{userId:guid}/reset-password")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.SuperAdmin}")]
    [Authorize(Policy = AuthorizationPolicies.RequireRecentMfa)]
    public async Task<IActionResult> ResetPassword(Guid userId, ResetUserPasswordRequest request, CancellationToken cancellationToken)
    {
        var isSuperAdmin = User.IsInRole(RoleNames.SuperAdmin);
        var target = await _userService.GetUserWithRolesAsync(userId, cancellationToken);

        if (target is null)
        {
            return NotFound();
        }

        var targetHasAdmin = target.UserRoles.Any(ur => ur.Role?.Name == RoleNames.Admin);
        var targetHasSuper = target.UserRoles.Any(ur => ur.Role?.Name == RoleNames.SuperAdmin);

        if (!isSuperAdmin && (targetHasAdmin || targetHasSuper))
        {
            return Forbid();
        }

        await _userService.ResetPasswordAsync(userId, request.NewPassword, cancellationToken);
        await AddUserAuditAsync(
            AuditActions.UserPasswordReset,
            userId,
            before: null,
            after: new { PasswordReset = true },
            cancellationToken);

        return NoContent();
    }

    [HttpDelete("{userId:guid}")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.SuperAdmin}")]
    [Authorize(Policy = AuthorizationPolicies.RequireRecentMfa)]
    public async Task<IActionResult> DeleteUser(Guid userId, CancellationToken cancellationToken)
    {
        var isSuperAdmin = User.IsInRole(RoleNames.SuperAdmin);
        var target = await _userService.GetUserWithRolesAsync(userId, cancellationToken);

        if (target is null)
        {
            return NotFound();
        }

        var targetHasAdmin = target.UserRoles.Any(ur => ur.Role?.Name == RoleNames.Admin);
        var targetHasSuper = target.UserRoles.Any(ur => ur.Role?.Name == RoleNames.SuperAdmin);

        if (!isSuperAdmin && (targetHasAdmin || targetHasSuper))
        {
            return Forbid();
        }

        var wasActive = target.IsActive;
        await _userService.DeactivateUserAsync(userId, cancellationToken);
        await AddUserAuditAsync(
            AuditActions.UserDeactivated,
            userId,
            new { IsActive = wasActive },
            new { IsActive = false },
            cancellationToken);

        return NoContent();
    }

    private async Task AddUserAuditAsync(
        string action,
        Guid targetUserId,
        object? before,
        object? after,
        CancellationToken cancellationToken)
    {
        _auditService.AddEntry(
            User.GetUserId(),
            action,
            EntityTypes.User,
            targetUserId,
            before,
            after);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IReadOnlyCollection<string> GetRoleNames(User user)
    {
        return NormalizeRoleNames(user.UserRoles
            .Select(userRole => userRole.Role?.Name)
            .Where(roleName => !string.IsNullOrWhiteSpace(roleName))!);
    }

    private static IReadOnlyCollection<string> NormalizeRoleNames(IEnumerable<string> roleNames)
    {
        return roleNames
            .Select(roleName => roleName.Trim())
            .Where(roleName => !string.IsNullOrWhiteSpace(roleName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(roleName => roleName)
            .ToList();
    }
}
