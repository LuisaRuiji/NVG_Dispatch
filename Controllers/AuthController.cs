using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NVGInventory.Contracts;
using NVGInventory.Security;
using System.IdentityModel.Tokens.Jwt;

namespace NVGInventory.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest("Username and password are required.");
        }

        var result = await _authService.TryLoginAsync(
            new LoginCommand(request.Username, request.Password),
            cancellationToken);

        if (result is null)
        {
            return Unauthorized("Invalid username or password.");
        }

        return Ok(new LoginResponse(result.AccessToken, result.UserId, result.Roles));
    }

    [HttpGet("me")]
    [Authorize]
    public ActionResult<CurrentUserResponse> Me()
    {
        var userId = User.GetUserId();
        var username = User.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value
                       ?? User.Identity?.Name
                       ?? string.Empty;
        var roles = User.Claims
            .Where(claim => claim.Type == "role")
            .Select(claim => claim.Value)
            .Distinct()
            .ToList();

        return Ok(new CurrentUserResponse(userId, username, roles));
    }
}
