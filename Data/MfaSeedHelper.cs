using Microsoft.EntityFrameworkCore;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Entities;
using NVGInventory.Security;

namespace NVGInventory.Data;

internal static class MfaSeedHelper
{
    public const string DevAdminMfaSecret = "WLBLKYI6P2CWFJBUJW6Q3IO2YYVR3WUS";

    public static async Task EnsureAdminMfaEnabledAsync(
        InventoryDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var adminUsers = await dbContext.Users
            .Include(user => user.UserRoles)
            .ThenInclude(userRole => userRole.Role)
            .Where(user => user.UserRoles.Any(userRole =>
                userRole.Role != null &&
                (userRole.Role.Name == RoleNames.Admin || userRole.Role.Name == RoleNames.SuperAdmin)))
            .ToListAsync(cancellationToken);

        if (adminUsers.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var user in adminUsers)
        {
            EnableSeededMfa(user, now);
        }
    }

    public static void EnableSeededMfa(User user, DateTime nowUtc)
    {
        user.MfaEnabled = true;
        user.MfaSecretKey = ProtectMfaSecret(DevAdminMfaSecret);
        user.PendingMfaSecretKey = null;
        user.MfaEnabledAt ??= nowUtc;
        user.MfaLastVerifiedAt ??= nowUtc;
    }

    private static string ProtectMfaSecret(string secretKey)
    {
        try
        {
            return SensitiveFieldProtector.Protect(secretKey) ?? secretKey;
        }
        catch (InvalidOperationException)
        {
            return secretKey;
        }
    }
}
