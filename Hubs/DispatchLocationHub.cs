using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Modules.Dispatching.Enums;

namespace NVGInventory.Hubs;

[Authorize]
public sealed class DispatchLocationHub : Hub<IDispatchLocationClient>
{
    private readonly InventoryDbContext _dbContext;

    public DispatchLocationHub(InventoryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public override async Task OnConnectedAsync()
    {
        var user = Context.User;

        // Block HeadOfFinance and InventoryOfficer from connecting
        if (user?.IsInRole(RoleNames.HeadOfFinance) == true ||
            user?.IsInRole(RoleNames.InventoryOfficer) == true)
        {
            Context.Abort();
            return;
        }

        if (user?.IsInRole(RoleNames.Dispatcher) == true)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "dispatch-live-map");
        }

        if (user?.IsInRole(RoleNames.Manager) == true)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "manager-live-map");
        }

        if (user?.IsInRole(RoleNames.Owner) == true)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "owner-live-map-readonly");
        }

        var userId = ResolveUserId(user);
        if (userId.HasValue && user?.IsInRole(RoleNames.Driver) == true)
        {
            var driver = await _dbContext.DispatchDrivers
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.UserId == userId.Value);

            if (driver != null)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"driver-{driver.Id}");

                var activeStatuses = new[]
                {
                    TripStatus.Dispatched,
                    TripStatus.EnroutePickup,
                    TripStatus.AtPickup,
                    TripStatus.Loaded,
                    TripStatus.EnrouteDropoff,
                    TripStatus.AtDropoff,
                    TripStatus.OnHold,
                    TripStatus.FailedAttempt,
                    TripStatus.Delivered
                };

                var activeTripIds = await _dbContext.DispatchTrips
                    .AsNoTracking()
                    .Where(t => t.DriverUserId == userId.Value && activeStatuses.Contains(t.Status))
                    .Select(t => t.Id)
                    .ToListAsync();

                foreach (var tripId in activeTripIds)
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"trip-{tripId}");
                }
            }
        }

        await base.OnConnectedAsync();
    }

    public async Task SubscribeToTrip(Guid tripId)
    {
        var user = Context.User;

        if (user?.IsInRole(RoleNames.HeadOfFinance) == true ||
            user?.IsInRole(RoleNames.InventoryOfficer) == true)
        {
            throw new HubException("Access denied.");
        }

        if (user?.IsInRole(RoleNames.Dispatcher) == true ||
            user?.IsInRole(RoleNames.Manager) == true ||
            user?.IsInRole(RoleNames.Owner) == true)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"trip-{tripId}");
            return;
        }

        var userId = ResolveUserId(user);
        if (userId.HasValue && user?.IsInRole(RoleNames.Driver) == true)
        {
            var isAssigned = await _dbContext.DispatchTrips
                .AsNoTracking()
                .AnyAsync(t => t.Id == tripId && t.DriverUserId == userId.Value);

            if (isAssigned)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"trip-{tripId}");
            }
            else
            {
                throw new HubException("Access denied to this trip.");
            }
            return;
        }

        throw new HubException("Access denied.");
    }

    private static Guid? ResolveUserId(ClaimsPrincipal? user)
    {
        var raw = user?.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? user?.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var userId) ? userId : null;
    }
}
