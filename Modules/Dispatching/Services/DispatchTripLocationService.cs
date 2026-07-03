using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Exceptions;
using NVGInventory.Hubs;
using NVGInventory.Hubs.Events;
using NVGInventory.Modules.Dispatching.Entities;
using NVGInventory.Modules.Dispatching.Enums;

namespace NVGInventory.Modules.Dispatching.Services;

public sealed record RecordTripLocationPingCommand(
    Guid TripId,
    double Latitude,
    double Longitude,
    double? AccuracyMeters,
    DateTime? RecordedAt);

public sealed record RecordedTripLocationPing(
    Guid Id,
    Guid TripId,
    Guid DriverId,
    double Latitude,
    double Longitude,
    double? AccuracyMeters,
    DateTime RecordedAt,
    DateTime CreatedAt);

public sealed class DispatchTripLocationService
{
    private static readonly TripStatus[] LocationPingStatuses =
    [
        TripStatus.Dispatched,
        TripStatus.EnroutePickup,
        TripStatus.AtPickup,
        TripStatus.Loaded,
        TripStatus.EnrouteDropoff,
        TripStatus.AtDropoff
    ];

    private readonly InventoryDbContext _dbContext;
    private readonly IHubContext<VaiaDispatchHub, IVaiaDispatchClient>? _hubContext;
    private readonly ILogger<DispatchTripLocationService>? _logger;

    public DispatchTripLocationService(
        InventoryDbContext dbContext,
        IHubContext<VaiaDispatchHub, IVaiaDispatchClient>? hubContext = null,
        ILogger<DispatchTripLocationService>? logger = null)
    {
        _dbContext = dbContext;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task<RecordedTripLocationPing> RecordLocationAsync(
        RecordTripLocationPingCommand command,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default)
    {
        if (!actor.IsDriver)
        {
            throw new ForbiddenDomainException("Driver role required.");
        }

        ValidateCoordinates(command.Latitude, command.Longitude);
        if (command.AccuracyMeters is < 0)
        {
            throw new BusinessRuleViolationException("AccuracyMeters cannot be negative.");
        }

        var trip = await _dbContext.DispatchTrips
            .Include(item => item.Driver)
            .Include(item => item.TruckAsset)
            .FirstOrDefaultAsync(item => item.Id == command.TripId, cancellationToken);

        if (trip is null)
        {
            throw new NotFoundException("Trip not found.");
        }

        if (trip.DriverUserId != actor.UserId)
        {
            throw new ForbiddenDomainException("Drivers can only send location for their own assigned trip.");
        }

        if (Array.IndexOf(LocationPingStatuses, trip.Status) < 0)
        {
            throw new ConflictDomainException("Location pings are only allowed for active dispatched trips.");
        }

        var now = DateTime.UtcNow;
        var recordedAt = command.RecordedAt ?? now;

        // Trip-scoped pings avoid off-duty/background tracking and keep the database as the source of truth.
        var ping = new TripLocationPing
        {
            Id = Guid.NewGuid(),
            TripId = trip.Id,
            DriverId = actor.UserId,
            Latitude = command.Latitude,
            Longitude = command.Longitude,
            AccuracyMeters = command.AccuracyMeters,
            RecordedAt = recordedAt,
            CreatedAt = now
        };

        _dbContext.DispatchTripLocationPings.Add(ping);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await BroadcastLocationAsync(trip, ping, cancellationToken);

        return new RecordedTripLocationPing(
            ping.Id,
            ping.TripId,
            ping.DriverId,
            ping.Latitude,
            ping.Longitude,
            ping.AccuracyMeters,
            ping.RecordedAt,
            ping.CreatedAt);
    }

    private async Task BroadcastLocationAsync(Trip trip, TripLocationPing ping, CancellationToken cancellationToken)
    {
        if (_hubContext is null)
        {
            return;
        }

        try
        {
            var e = new DriverLocationUpdatedEvent(
                trip.Id,
                ping.DriverId,
                trip.Driver?.Username ?? "Assigned driver",
                trip.TruckAssetId,
                trip.TruckAsset?.PlateNo ?? trip.TruckAsset?.AssetCode,
                ping.Latitude,
                ping.Longitude,
                ping.AccuracyMeters,
                ping.RecordedAt,
                DispatchEventFormatting.TripStatus(trip.Status));

            await _hubContext.Clients
                .Group(VaiaDispatchHub.DispatchLocationGroup)
                .DriverLocationUpdated(e);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to broadcast driver location for trip {TripId}.", trip.Id);
        }
    }

    private static void ValidateCoordinates(double latitude, double longitude)
    {
        if (double.IsNaN(latitude) || double.IsInfinity(latitude) || latitude is < -90 or > 90)
        {
            throw new BusinessRuleViolationException("Latitude must be between -90 and 90.");
        }

        if (double.IsNaN(longitude) || double.IsInfinity(longitude) || longitude is < -180 or > 180)
        {
            throw new BusinessRuleViolationException("Longitude must be between -180 and 180.");
        }
    }
}
