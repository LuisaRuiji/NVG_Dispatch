using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace NVGInventory.Security;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var idValue = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
                      ?? user.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(idValue))
        {
            throw new InvalidOperationException("Authenticated user id claim is missing.");
        }

        return Guid.Parse(idValue);
    }
}
