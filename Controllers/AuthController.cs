using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NVGInventory.Contracts;
using NVGInventory.Domain.Constants;
using NVGInventory.Security;
using System.IdentityModel.Tokens.Jwt;

namespace NVGInventory.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly AuthEventService _authEventService;

    public AuthController(AuthService authService, AuthEventService authEventService)
    {
        _authService = authService;
        _authEventService = authEventService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            var attempt = new AuthAttemptResult(
                false,
                null,
                AuthFailureReasons.ValidationError,
                null,
                request.Username?.Trim(),
                Array.Empty<string>());
            await _authEventService.LogLoginAttemptAsync(
                new LoginCommand(request.Username ?? string.Empty, request.Password ?? string.Empty),
                attempt,
                HttpContext,
                cancellationToken);
            return BadRequest("Username and password are required.");
        }

        var attemptResult = await _authService.TryLoginAsync(
            new LoginCommand(request.Username, request.Password),
            cancellationToken);

        await _authEventService.LogLoginAttemptAsync(
            new LoginCommand(request.Username, request.Password),
            attemptResult,
            HttpContext,
            cancellationToken);

        if (!attemptResult.Success || attemptResult.Result is null)
        {
            return Unauthorized("Invalid username or password.");
        }

        var result = attemptResult.Result;
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
