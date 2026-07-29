using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using NVGInventory.Contracts;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Services;
using NVGInventory.Security;
using System.IdentityModel.Tokens.Jwt;

namespace NVGInventory.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private const string RefreshTokenCookieName = "nvg_refresh_token";
    private const string CsrfHeaderName = "X-VAIA-CSRF";
    private const string CsrfHeaderValue = "1";

    private readonly AuthService _authService;
    private readonly AuthEventService _authEventService;
    private readonly IAuditService _auditService;
    private readonly UserService _userService;
    private readonly InventoryDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;

    public AuthController(
        AuthService authService,
        AuthEventService authEventService,
        IAuditService auditService,
        UserService userService,
        InventoryDbContext dbContext,
        IWebHostEnvironment environment)
    {
        _authService = authService;
        _authEventService = authEventService;
        _auditService = auditService;
        _userService = userService;
        _dbContext = dbContext;
        _environment = environment;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<ActionResult<object>> Login(LoginRequest request, CancellationToken cancellationToken)
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
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            request.RememberMe,
            cancellationToken);

        await _authEventService.LogLoginAttemptAsync(
            new LoginCommand(request.Username, request.Password),
            attemptResult,
            HttpContext,
            cancellationToken);

        if (attemptResult.RequiresMfa && attemptResult.MfaChallenge is not null)
        {
            return Ok(new MfaRequiredResponse(
                true,
                attemptResult.MfaChallenge.ChallengeId,
                attemptResult.MfaChallenge.Method,
                attemptResult.MfaChallenge.ExpiresAtUtc));
        }

        if (!attemptResult.Success || attemptResult.Result is null)
        {
            return Unauthorized("Invalid username or password.");
        }

        var result = attemptResult.Result;
        SetRefreshTokenCookie(result);

        return Ok(new LoginResponse(
            result.AccessToken,
            result.UserId,
            result.Roles,
            result.ExpiresAtUtc,
            result.MustChangePassword));
    }

    [HttpPost("mfa/verify")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<ActionResult<LoginResponse>> VerifyMfa(
        MfaVerifyRequest request,
        CancellationToken cancellationToken)
    {
        var attempt = await _authService.TryVerifyMfaLoginAsync(
            request.ChallengeId,
            request.Code,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            request.RememberMe,
            cancellationToken);

        await _authEventService.LogMfaVerifyAttemptAsync(attempt, HttpContext, cancellationToken);

        if (!attempt.Success || attempt.Result is null)
        {
            if (attempt.FailureReason == AuthFailureReasons.MfaChallengeExpired)
            {
                return BadRequest("MFA challenge expired. Please sign in again.");
            }

            return Unauthorized("Invalid MFA code.");
        }

        var result = attempt.Result;
        SetRefreshTokenCookie(result);

        return Ok(new LoginResponse(
            result.AccessToken,
            result.UserId,
            result.Roles,
            result.ExpiresAtUtc,
            result.MustChangePassword));
    }

    [HttpPost("step-up")]
    [Authorize]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<ActionResult<StepUpResponse>> StepUp(
        StepUpRequest request,
        CancellationToken cancellationToken)
    {
        var attempt = await _authService.TryStepUpAsync(
            User.GetUserId(),
            request.Code,
            cancellationToken);

        await _authEventService.LogStepUpAttemptAsync(attempt, HttpContext, cancellationToken);

        if (!attempt.Success || attempt.Result is null)
        {
            if (attempt.FailureReason == AuthFailureReasons.MfaNotEnabled)
            {
                return BadRequest("MFA is not enabled for this account.");
            }

            return Unauthorized("Invalid MFA code.");
        }

        return Ok(new StepUpResponse(
            attempt.Result.AccessToken,
            attempt.Result.UserId,
            attempt.Result.Roles,
            attempt.Result.ExpiresAtUtc));
    }

    [HttpGet("mfa")]
    [Authorize]
    public async Task<ActionResult<MfaStatusResponse>> GetMfaStatus(CancellationToken cancellationToken)
    {
        var status = await _authService.GetMfaStatusAsync(User.GetUserId(), cancellationToken);
        if (status is null)
        {
            return NotFound();
        }

        return Ok(new MfaStatusResponse(status.Enabled, status.EnabledAt, status.LastVerifiedAt));
    }

    [HttpPost("mfa/setup")]
    [Authorize]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<ActionResult<MfaSetupResponse>> SetupMfa(CancellationToken cancellationToken)
    {
        var result = await _authService.BeginMfaEnrollmentAsync(User.GetUserId(), cancellationToken);
        if (result is null)
        {
            return NotFound();
        }

        return Ok(new MfaSetupResponse(result.SecretKey, result.OtpAuthUri));
    }

    [HttpPost("mfa/confirm")]
    [Authorize]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<ActionResult<MfaStatusResponse>> ConfirmMfa(
        MfaConfirmRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.ConfirmMfaEnrollmentAsync(
            User.GetUserId(),
            request.Code,
            cancellationToken);

        if (!result.Success || result.Status is null)
        {
            return result.FailureReason == AuthFailureReasons.InvalidMfaCode
                ? Unauthorized("Invalid MFA code.")
                : BadRequest("Unable to enable MFA.");
        }

        return Ok(new MfaStatusResponse(
            result.Status.Enabled,
            result.Status.EnabledAt,
            result.Status.LastVerifiedAt));
    }

    [HttpPost("mfa/disable")]
    [Authorize]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<ActionResult<MfaStatusResponse>> DisableMfa(
        MfaDisableRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.DisableMfaAsync(
            User.GetUserId(),
            request.Code,
            cancellationToken);

        if (!result.Success || result.Status is null)
        {
            return result.FailureReason == AuthFailureReasons.InvalidMfaCode
                ? Unauthorized("Invalid MFA code.")
                : BadRequest("Unable to disable MFA.");
        }

        return Ok(new MfaStatusResponse(
            result.Status.Enabled,
            result.Status.EnabledAt,
            result.Status.LastVerifiedAt));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<ActionResult<RefreshTokenResponse>> Refresh(
        RefreshTokenRequest? request,
        CancellationToken cancellationToken)
    {
        var usesCookie = UsesRefreshTokenCookie(request);
        if (usesCookie && !HasCsrfHeader())
        {
            return StatusCode(StatusCodes.Status403Forbidden, "CSRF header is required.");
        }

        var refreshToken = GetRefreshToken(request);
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return BadRequest("Refresh token is required.");
        }

        var attempt = await _authService.TryRefreshAsync(
            refreshToken,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            cancellationToken);

        if (!attempt.Success || attempt.Result is null)
        {
            if (usesCookie)
            {
                ClearRefreshTokenCookie();
            }

            return Unauthorized("Invalid refresh token.");
        }

        var result = attempt.Result;
        SetRefreshTokenCookie(result);

        return Ok(new RefreshTokenResponse(
            result.AccessToken,
            result.UserId,
            result.Roles,
            result.ExpiresAtUtc,
            result.MustChangePassword));
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(LogoutRequest? request, CancellationToken cancellationToken)
    {
        var usesCookie = UsesRefreshTokenCookie(request);
        if (usesCookie && !HasCsrfHeader())
        {
            return StatusCode(StatusCodes.Status403Forbidden, "CSRF header is required.");
        }

        var refreshToken = GetRefreshToken(request);
        await _authService.RevokeRefreshTokenAsync(
            refreshToken,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);

        ClearRefreshTokenCookie();
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<CurrentUserResponse>> Me(CancellationToken cancellationToken)
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

        var userFlags = await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new { user.Email, user.MfaEnabled, user.MustChangePassword })
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(new CurrentUserResponse(
            userId,
            username,
            userFlags?.Email,
            roles,
            userFlags?.MfaEnabled ?? false,
            userFlags?.MustChangePassword ?? false));
    }

    [HttpPatch("me/profile")]
    [Authorize]
    public async Task<ActionResult<CurrentUserResponse>> UpdateMyProfile(
        UpdateUserProfileRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var user = await _userService.GetUserWithRolesAsync(userId, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        var before = new { user.Username, user.Email };
        await _userService.UpdateUserProfileAsync(
            userId,
            new UpdateUserProfileCommand(request.Username, request.Email),
            cancellationToken);

        _auditService.AddEntry(
            userId,
            AuditActions.UserProfileUpdated,
            EntityTypes.User,
            userId,
            before,
            new { Username = request.Username.Trim(), Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim() });
        await _dbContext.SaveChangesAsync(cancellationToken);

        var roles = user.UserRoles
            .Select(role => role.Role?.Name ?? string.Empty)
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Distinct()
            .ToList();
        return Ok(new CurrentUserResponse(
            userId,
            request.Username.Trim(),
            string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            roles,
            user.MfaEnabled,
            user.MustChangePassword));
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await _authService.ChangePasswordAsync(
            userId,
            request.NewPassword,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);

        _auditService.AddEntry(
            userId,
            AuditActions.UserPasswordChanged,
            EntityTypes.User,
            userId,
            null,
            new { MustChangePassword = false });

        await _dbContext.SaveChangesAsync(cancellationToken);
        ClearRefreshTokenCookie();
        return NoContent();
    }

    private string? GetRefreshToken(RefreshTokenRequest? request)
    {
        return string.IsNullOrWhiteSpace(request?.RefreshToken)
            ? Request.Cookies[RefreshTokenCookieName]
            : request.RefreshToken;
    }

    private string? GetRefreshToken(LogoutRequest? request)
    {
        return string.IsNullOrWhiteSpace(request?.RefreshToken)
            ? Request.Cookies[RefreshTokenCookieName]
            : request.RefreshToken;
    }

    private bool UsesRefreshTokenCookie(RefreshTokenRequest? request)
    {
        return string.IsNullOrWhiteSpace(request?.RefreshToken)
               && Request.Cookies.ContainsKey(RefreshTokenCookieName);
    }

    private bool UsesRefreshTokenCookie(LogoutRequest? request)
    {
        return string.IsNullOrWhiteSpace(request?.RefreshToken)
               && Request.Cookies.ContainsKey(RefreshTokenCookieName);
    }

    private bool HasCsrfHeader()
    {
        return Request.Headers.TryGetValue(CsrfHeaderName, out var values)
               && values.Any(value => string.Equals(value, CsrfHeaderValue, StringComparison.Ordinal));
    }

    private void SetRefreshTokenCookie(AuthResult result)
    {
        Response.Cookies.Append(
            RefreshTokenCookieName,
            result.RefreshToken,
            BuildRefreshCookieOptions(result.RefreshTokenExpiresAtUtc, result.RefreshTokenIsPersistent));
    }

    private void ClearRefreshTokenCookie()
    {
        Response.Cookies.Delete(RefreshTokenCookieName, BuildRefreshCookieDeleteOptions());
    }

    private CookieOptions BuildRefreshCookieOptions(DateTime expiresAtUtc, bool isPersistent)
    {
        var options = new CookieOptions
        {
            HttpOnly = true,
            Secure = !_environment.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Path = "/api/auth"
        };

        if (isPersistent)
        {
            var expires = new DateTimeOffset(DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc));
            options.Expires = expires;
            options.MaxAge = expires - DateTimeOffset.UtcNow;
        }

        return options;
    }

    private CookieOptions BuildRefreshCookieDeleteOptions()
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = !_environment.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Path = "/api/auth"
        };
    }
}
