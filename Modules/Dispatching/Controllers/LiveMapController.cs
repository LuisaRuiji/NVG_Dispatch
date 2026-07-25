using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NVGInventory.Security;
using NVGInventory.Domain.Constants;
using NVGInventory.Modules.Dispatching.Services;
using NVGInventory.Domain.Exceptions;

namespace NVGInventory.Modules.Dispatching.Controllers;

[ApiController]
public sealed class LiveMapController : ControllerBase
{
    private readonly LocationTrackingService _trackingService;

    public LiveMapController(LocationTrackingService trackingService)
    {
        _trackingService = trackingService;
    }

    [HttpGet("api/dispatch/live-map")]
    [Authorize(Roles = RoleNames.Dispatcher + "," + RoleNames.Manager + "," + RoleNames.Owner)]
    public async Task<IActionResult> GetLiveMapSnapshot(CancellationToken cancellationToken)
    {
        var snapshot = await _trackingService.GetLiveMapSnapshotAsync(cancellationToken);
        return Ok(snapshot);
    }

    [HttpGet("api/driver/my-route-map")]
    [Authorize(Roles = RoleNames.Driver)]
    public async Task<IActionResult> GetDriverMyRouteMap(CancellationToken cancellationToken)
    {
        try
        {
            var driverUserId = User.GetUserId();
            var routeMap = await _trackingService.GetDriverActiveRouteMapAsync(driverUserId, cancellationToken);
            return Ok(routeMap);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
