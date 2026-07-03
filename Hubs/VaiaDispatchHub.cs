using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using NVGInventory.Domain.Constants;

namespace NVGInventory.Hubs;

[Authorize]
public sealed class VaiaDispatchHub : Hub<IVaiaDispatchClient>
{
    public const string DispatchOpsGroup = "DispatchOps";
    public const string DispatchLocationGroup = "DispatchLocations";

    public static string DriverGroup(Guid userId) => $"Driver:{userId}";

    public static string CustomerGroup(Guid userId) => $"Customer:{userId}";

    public override async Task OnConnectedAsync()
    {
        var user = Context.User;
        if (user?.IsInRole(RoleNames.Dispatcher) == true ||
            user?.IsInRole(RoleNames.Manager) == true ||
            user?.IsInRole(RoleNames.Admin) == true ||
            user?.IsInRole(RoleNames.SuperAdmin) == true ||
            user?.IsInRole(RoleNames.Ceo) == true)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, DispatchOpsGroup);
        }

        if (user?.IsInRole(RoleNames.Dispatcher) == true ||
            user?.IsInRole(RoleNames.Manager) == true)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, DispatchLocationGroup);
        }

        var userId = ResolveUserId(user);
        if (userId.HasValue && user?.IsInRole(RoleNames.Driver) == true)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, DriverGroup(userId.Value));
        }

        if (userId.HasValue && user?.IsInRole(RoleNames.Customer) == true)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, CustomerGroup(userId.Value));
        }

        await base.OnConnectedAsync();
    }

    private static Guid? ResolveUserId(ClaimsPrincipal? user)
    {
        var raw = user?.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? user?.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var userId) ? userId : null;
    }
}
