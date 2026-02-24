using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NVGInventory.Domain.Constants;
using NVGInventory.Contracts;
using NVGInventory.Domain.Services;

namespace NVGInventory.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public sealed class UsersController : ControllerBase
{
    private readonly UserService _userService;

    public UsersController(UserService userService)
    {
        _userService = userService;
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
    public async Task<ActionResult<CreateUserResponse>> CreateUser(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await _userService.CreateUserAsync(
            new CreateUserCommand(request.Username, request.Password, request.Email),
            cancellationToken);

        return Ok(new CreateUserResponse(user.Id, user.Username));
    }

    [HttpPost("{userId:guid}/roles")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.SuperAdmin}")]
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

        await _userService.AssignRoleAsync(userId, request.RoleName, cancellationToken);
        return NoContent();
    }

    [HttpPut("{userId:guid}/roles")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.SuperAdmin}")]
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

        await _userService.ReplaceRolesAsync(userId, request.RoleNames, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{userId:guid}/status")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.SuperAdmin}")]
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

        await _userService.UpdateUserStatusAsync(userId, request.IsActive, cancellationToken);
        return NoContent();
    }

    [HttpPost("{userId:guid}/reset-password")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.SuperAdmin}")]
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
        return NoContent();
    }

    [HttpDelete("{userId:guid}")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.SuperAdmin}")]
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

        await _userService.DeactivateUserAsync(userId, cancellationToken);
        return NoContent();
    }
}
