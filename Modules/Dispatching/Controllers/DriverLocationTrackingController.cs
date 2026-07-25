using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NVGInventory.Security;
using NVGInventory.Domain.Constants;
using NVGInventory.Modules.Dispatching.Services;
using NVGInventory.Modules.Dispatching.Enums;

namespace NVGInventory.Modules.Dispatching.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.Driver)]
public sealed class DriverLocationTrackingController : ControllerBase
{
    private readonly LocationTrackingService _trackingService;

    public DriverLocationTrackingController(LocationTrackingService trackingService)
    {
        _trackingService = trackingService;
    }

    [HttpPost("api/driver/trips/{tripId:guid}/start-tracking")]
    public async Task<IActionResult> StartTracking(
        Guid tripId,
        [FromBody] StartTrackingRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            var driverUserId = User.GetUserId();
            var session = await _trackingService.StartTrackingSessionAsync(
                tripId,
                driverUserId,
                request?.Latitude,
                request?.Longitude,
                cancellationToken);

            return Ok(new
            {
                session.Id,
                session.TripId,
                session.DispatchDriverId,
                session.DispatchTruckId,
                session.StartedAt,
                session.Status,
                session.StartLatitude,
                session.StartLongitude
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("api/driver/location")]
    public async Task<IActionResult> SendLocation(
        [FromBody] SendLocationRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return BadRequest(new { message = "Request body is required." });
        }

        try
        {
            var driverUserId = User.GetUserId();
            var update = await _trackingService.SaveLocationUpdateAsync(
                driverUserId,
                request.TrackingSessionId,
                request.TripId,
                request.DispatchDriverId,
                request.DispatchTruckId,
                request.Latitude,
                request.Longitude,
                request.AccuracyMeters,
                request.SpeedKph,
                request.Heading,
                request.RecordedAt,
                request.Source,
                cancellationToken);

            return Ok(new
            {
                update.Id,
                update.TrackingSessionId,
                update.TripId,
                update.DispatchDriverId,
                update.DispatchTruckId,
                update.Latitude,
                update.Longitude,
                update.RecordedAt,
                update.ReceivedAt,
                update.Source
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("api/driver/trips/{tripId:guid}/stop-tracking")]
    public async Task<IActionResult> StopTracking(
        Guid tripId,
        [FromBody] StopTrackingRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            var driverUserId = User.GetUserId();
            var session = await _trackingService.StopTrackingSessionAsync(
                tripId,
                driverUserId,
                request?.Latitude,
                request?.Longitude,
                cancellationToken);

            return Ok(new
            {
                session.Id,
                session.TripId,
                session.Status,
                session.EndLatitude,
                session.EndLongitude,
                session.EndedAt
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

public sealed record StartTrackingRequest(decimal? Latitude, decimal? Longitude);

public sealed record SendLocationRequest(
    Guid? TrackingSessionId,
    Guid? TripId,
    Guid? DispatchDriverId,
    Guid? DispatchTruckId,
    decimal Latitude,
    decimal Longitude,
    decimal? AccuracyMeters,
    decimal? SpeedKph,
    decimal? Heading,
    DateTime RecordedAt,
    LocationUpdateSource Source
);

public sealed record StopTrackingRequest(decimal? Latitude, decimal? Longitude);
