using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace NVGInventory.Security;

public sealed class RecentMfaRequirement : IAuthorizationRequirement
{
}

public sealed class RecentMfaRequirementHandler : AuthorizationHandler<RecentMfaRequirement>
{
    private readonly MfaOptions _options;

    public RecentMfaRequirementHandler(IOptions<MfaOptions> options)
    {
        _options = options.Value;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RecentMfaRequirement requirement)
    {
        var hasMfaMethod = context.User.Claims.Any(claim =>
            claim.Type == "amr" && string.Equals(claim.Value, "mfa", StringComparison.OrdinalIgnoreCase));

        var mfaAtClaim = context.User.FindFirst("mfa_at")?.Value;
        if (!hasMfaMethod || string.IsNullOrWhiteSpace(mfaAtClaim))
        {
            return Task.CompletedTask;
        }

        if (!long.TryParse(mfaAtClaim, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixSeconds))
        {
            return Task.CompletedTask;
        }

        var mfaAt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
        var minutes = _options.StepUpMinutes <= 0 ? 15 : _options.StepUpMinutes;
        if (mfaAt >= DateTime.UtcNow.AddMinutes(-minutes))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
