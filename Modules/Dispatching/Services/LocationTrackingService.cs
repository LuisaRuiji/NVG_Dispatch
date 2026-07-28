using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Exceptions;
using NVGInventory.Domain.Services;
using NVGInventory.Hubs;
using NVGInventory.Modules.Dispatching.Entities;
using NVGInventory.Modules.Dispatching.Enums;

namespace NVGInventory.Modules.Dispatching.Services;

public sealed record LiveMapTripResponse(
    Guid TripId,
    Guid? DispatchTruckId,
    string PlateNumber,
    Guid? DispatchDriverId,
    string DriverName,
    string CurrentTripStatus,
    decimal? LastLatitude,
    decimal? LastLongitude,
    DateTime? LastLocationAt,
    string? PickupLocation,
    decimal? PickupLatitude,
    decimal? PickupLongitude,
    DateTime? PickupScheduledAt,
    string? DropoffLocation,
    decimal? DropoffLatitude,
    decimal? DropoffLongitude,
    DateTime? DropoffScheduledAt,
    bool DelayFlag);

public sealed class LocationTrackingService
{
    private readonly InventoryDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly IHubContext<DispatchLocationHub, IDispatchLocationClient> _hubContext;
    private readonly IGeocodingService? _geocodingService;

    public LocationTrackingService(
        InventoryDbContext dbContext,
        IAuditService auditService,
        IHubContext<DispatchLocationHub, IDispatchLocationClient> hubContext,
        IGeocodingService? geocodingService = null)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _hubContext = hubContext;
        _geocodingService = geocodingService;
    }

    public async Task<LocationTrackingSession> StartTrackingSessionAsync(
        Guid tripId,
        Guid driverUserId,
        decimal? startLatitude,
        decimal? startLongitude,
        CancellationToken cancellationToken = default)
    {
        var trip = await _dbContext.DispatchTrips
            .Include(t => t.Driver)
            .Include(t => t.TruckAsset)
            .FirstOrDefaultAsync(t => t.Id == tripId, cancellationToken);

        if (trip == null)
        {
            throw new NotFoundException("Trip not found.");
        }

        if (trip.DriverUserId != driverUserId)
        {
            throw new ForbiddenDomainException("Driver is not assigned to this trip.");
        }

        if (trip.Status == TripStatus.Draft ||
            trip.Status == TripStatus.Cancelled ||
            trip.Status == TripStatus.Closed ||
            trip.Status == TripStatus.Delivered)
        {
            throw new InvalidOperationException("Trip is not active or is already completed.");
        }

        var existingSession = await _dbContext.LocationTrackingSessions
            .FirstOrDefaultAsync(s => s.TripId == tripId && s.Status == TrackingSessionStatus.Active, cancellationToken);

        if (existingSession != null)
        {
            return existingSession;
        }

        var driver = await _dbContext.DispatchDrivers
            .FirstOrDefaultAsync(d => d.UserId == driverUserId, cancellationToken);
        if (driver == null)
        {
            driver = new Driver
            {
                Id = Guid.NewGuid(),
                UserId = driverUserId,
                LicenseNumber = "Pending",
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.DispatchDrivers.Add(driver);
            // We don't call SaveChanges here yet, it will be saved at the end of the method
        }

        if (trip.TruckAssetId == null)
        {
            throw new InvalidOperationException("No truck assigned to this trip.");
        }
        var truck = await _dbContext.DispatchTrucks
            .FirstOrDefaultAsync(t => t.AssetId == trip.TruckAssetId.Value, cancellationToken);
        if (truck == null)
        {
            truck = new Truck
            {
                Id = Guid.NewGuid(),
                AssetId = trip.TruckAssetId.Value,
                PlateNumber = trip.TruckAsset?.PlateNo ?? trip.TruckAsset?.AssetCode ?? "Unknown",
                ContainerCapability = "Unknown",
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.DispatchTrucks.Add(truck);
        }

        if (startLatitude.HasValue && startLongitude.HasValue)
        {
            ValidateCoordinates(startLatitude.Value, startLongitude.Value);
        }

        var now = DateTime.UtcNow;
        var session = new LocationTrackingSession
        {
            Id = Guid.NewGuid(),
            TripId = tripId,
            DispatchDriverId = driver.Id,
            DispatchTruckId = truck.Id,
            StartedAt = now,
            Status = TrackingSessionStatus.Active,
            StartLatitude = startLatitude,
            StartLongitude = startLongitude,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.LocationTrackingSessions.Add(session);

        if (startLatitude.HasValue && startLongitude.HasValue)
        {
            truck.LastLatitude = startLatitude.Value;
            truck.LastLongitude = startLongitude.Value;
            truck.LastLocationAt = now;

            trip.LastLatitude = startLatitude.Value;
            trip.LastLongitude = startLongitude.Value;
            trip.LastLocationAt = now;
        }

        _auditService.AddEntry(
            driverUserId,
            AuditActions.LocationTrackingStarted,
            EntityTypes.LocationTrackingSession,
            session.Id,
            before: null,
            after: new
            {
                session.Id,
                session.TripId,
                session.DispatchDriverId,
                session.DispatchTruckId,
                session.Status,
                session.StartLatitude,
                session.StartLongitude
            },
            tripId: tripId);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return session;
    }

    public async Task<DriverLocationUpdate> SaveLocationUpdateAsync(
        Guid driverUserId,
        Guid? trackingSessionId,
        Guid? tripId,
        Guid? dispatchDriverId,
        Guid? dispatchTruckId,
        decimal latitude,
        decimal longitude,
        decimal? accuracyMeters,
        decimal? speedKph,
        decimal? heading,
        DateTime recordedAt,
        LocationUpdateSource source,
        CancellationToken cancellationToken = default)
    {
        ValidateCoordinates(latitude, longitude);

        Trip trip;
        if (tripId.HasValue)
        {
            trip = await _dbContext.DispatchTrips
                .Include(t => t.Driver)
                .Include(t => t.TruckAsset)
                .FirstOrDefaultAsync(t => t.Id == tripId.Value, cancellationToken);
        }
        else
        {
            if (!trackingSessionId.HasValue)
            {
                throw new InvalidOperationException("Either TripId or TrackingSessionId must be provided.");
            }
            var sessionTemp = await _dbContext.LocationTrackingSessions
                .Include(s => s.Trip)
                .FirstOrDefaultAsync(s => s.Id == trackingSessionId.Value, cancellationToken);
            trip = sessionTemp?.Trip;
        }

        if (trip == null)
        {
            throw new NotFoundException("Trip not found.");
        }

        if (trip.DriverUserId != driverUserId)
        {
            throw new ForbiddenDomainException("Driver is not assigned to this trip.");
        }

        if (trip.Status == TripStatus.Draft ||
            trip.Status == TripStatus.Cancelled ||
            trip.Status == TripStatus.Closed ||
            trip.Status == TripStatus.Delivered)
        {
            throw new InvalidOperationException("Trip is not active or is already completed.");
        }

        var session = await _dbContext.LocationTrackingSessions
            .FirstOrDefaultAsync(s =>
                (trackingSessionId.HasValue ? s.Id == trackingSessionId.Value : s.TripId == trip.Id) &&
                s.Status == TrackingSessionStatus.Active, cancellationToken);

        if (session == null)
        {
            throw new InvalidOperationException("No active tracking session found for this trip.");
        }

        var driver = await _dbContext.DispatchDrivers
            .FirstOrDefaultAsync(d => d.UserId == driverUserId, cancellationToken);
        if (driver == null || session.DispatchDriverId != driver.Id)
        {
            throw new InvalidOperationException("Driver profile mismatch.");
        }

        if (trip.TruckAssetId == null)
        {
            throw new InvalidOperationException("No truck assigned to this trip.");
        }
        var truck = await _dbContext.DispatchTrucks
            .FirstOrDefaultAsync(t => t.AssetId == trip.TruckAssetId.Value, cancellationToken);
        if (truck == null || session.DispatchTruckId != truck.Id)
        {
            throw new InvalidOperationException("Truck profile mismatch.");
        }

        if (dispatchDriverId.HasValue && dispatchDriverId.Value != driver.Id)
        {
            throw new InvalidOperationException("Cannot spoof other driver identity.");
        }
        if (dispatchTruckId.HasValue && dispatchTruckId.Value != truck.Id)
        {
            throw new InvalidOperationException("Cannot spoof other truck identity.");
        }
        if (tripId.HasValue && tripId.Value != trip.Id)
        {
            throw new InvalidOperationException("Cannot spoof other trip identity.");
        }

        var now = DateTime.UtcNow;
        var update = new DriverLocationUpdate
        {
            Id = Guid.NewGuid(),
            TrackingSessionId = session.Id,
            TripId = trip.Id,
            DispatchDriverId = driver.Id,
            DispatchTruckId = truck.Id,
            Latitude = latitude,
            Longitude = longitude,
            AccuracyMeters = accuracyMeters,
            SpeedKph = speedKph,
            Heading = heading,
            RecordedAt = recordedAt,
            ReceivedAt = now,
            Source = source
        };

        _dbContext.DriverLocationUpdates.Add(update);

        truck.LastLatitude = latitude;
        truck.LastLongitude = longitude;
        truck.LastLocationAt = now;

        trip.LastLatitude = latitude;
        trip.LastLongitude = longitude;
        trip.LastLocationAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Broadcast update via SignalR
        var payload = new LocationUpdateBroadcastPayload(
            TripId: trip.Id,
            DispatchTruckId: truck.Id,
            TruckPlateNumber: truck.PlateNumber ?? "Unknown",
            DispatchDriverId: driver.Id,
            DriverUserId: trip.DriverUserId ?? Guid.Empty,
            Latitude: latitude,
            Longitude: longitude,
            AccuracyMeters: accuracyMeters,
            SpeedKph: speedKph,
            Heading: heading,
            TripStatus: trip.Status.ToString().ToUpper(),
            RecordedAt: recordedAt,
            ReceivedAt: now
        );

        await _hubContext.Clients.Group("dispatch-live-map").ReceiveLocationUpdate(payload);
        await _hubContext.Clients.Group("manager-live-map").ReceiveLocationUpdate(payload);
        await _hubContext.Clients.Group("owner-live-map-readonly").ReceiveLocationUpdate(payload);
        await _hubContext.Clients.Group($"trip-{trip.Id}").ReceiveLocationUpdate(payload);
        await _hubContext.Clients.Group($"driver-{driver.Id}").ReceiveLocationUpdate(payload);

        return update;
    }

    public async Task<LocationTrackingSession> StopTrackingSessionAsync(
        Guid tripId,
        Guid driverUserId,
        decimal? endLatitude,
        decimal? endLongitude,
        CancellationToken cancellationToken = default)
    {
        var trip = await _dbContext.DispatchTrips
            .Include(t => t.Driver)
            .Include(t => t.TruckAsset)
            .FirstOrDefaultAsync(t => t.Id == tripId, cancellationToken);

        if (trip == null)
        {
            throw new NotFoundException("Trip not found.");
        }

        if (trip.DriverUserId != driverUserId)
        {
            throw new ForbiddenDomainException("Driver is not assigned to this trip.");
        }

        var session = await _dbContext.LocationTrackingSessions
            .FirstOrDefaultAsync(s => s.TripId == tripId && s.Status == TrackingSessionStatus.Active, cancellationToken);

        if (session == null)
        {
            throw new InvalidOperationException("No active tracking session found.");
        }

        if (endLatitude.HasValue && endLongitude.HasValue)
        {
            ValidateCoordinates(endLatitude.Value, endLongitude.Value);
        }

        var now = DateTime.UtcNow;
        session.EndedAt = now;
        session.EndLatitude = endLatitude;
        session.EndLongitude = endLongitude;

        if (trip.Status == TripStatus.Delivered || trip.Status == TripStatus.Closed)
        {
            session.Status = TrackingSessionStatus.Completed;
        }
        else if (trip.Status == TripStatus.Cancelled)
        {
            session.Status = TrackingSessionStatus.Cancelled;
        }
        else
        {
            session.Status = TrackingSessionStatus.Stopped;
        }

        session.UpdatedAt = now;

        if (endLatitude.HasValue && endLongitude.HasValue)
        {
            if (trip.TruckAssetId.HasValue)
            {
                var truck = await _dbContext.DispatchTrucks
                    .FirstOrDefaultAsync(t => t.AssetId == trip.TruckAssetId.Value, cancellationToken);
                if (truck != null)
                {
                    truck.LastLatitude = endLatitude.Value;
                    truck.LastLongitude = endLongitude.Value;
                    truck.LastLocationAt = now;
                }
            }

            trip.LastLatitude = endLatitude.Value;
            trip.LastLongitude = endLongitude.Value;
            trip.LastLocationAt = now;
        }

        _auditService.AddEntry(
            driverUserId,
            AuditActions.LocationTrackingStopped,
            EntityTypes.LocationTrackingSession,
            session.Id,
            before: new { session.Id, Status = TrackingSessionStatus.Active },
            after: new
            {
                session.Id,
                session.TripId,
                session.Status,
                session.EndLatitude,
                session.EndLongitude,
                session.EndedAt
            },
            tripId: tripId);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return session;
    }

    public async Task<List<LiveMapTripResponse>> GetLiveMapSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var activeStatuses = new[]
        {
            TripStatus.Draft,
            TripStatus.Dispatched,
            TripStatus.EnroutePickup,
            TripStatus.AtPickup,
            TripStatus.Loaded,
            TripStatus.EnrouteDropoff,
            TripStatus.AtDropoff,
            TripStatus.OnHold,
            TripStatus.FailedAttempt
        };

        var trips = await _dbContext.DispatchTrips
            .AsNoTracking()
            .Include(t => t.Driver)
            .Include(t => t.TruckAsset)
            .Include(t => t.Stops)
            .Where(t => activeStatuses.Contains(t.Status))
            .ToListAsync(cancellationToken);

        var response = new List<LiveMapTripResponse>();

        foreach (var trip in trips)
        {
            Truck? truck = null;
            if (trip.TruckAssetId.HasValue)
            {
                truck = await _dbContext.DispatchTrucks
                    .AsNoTracking()
                    .FirstOrDefaultAsync(tr => tr.AssetId == trip.TruckAssetId.Value, cancellationToken);
            }

            Driver? driver = null;
            if (trip.DriverUserId.HasValue)
            {
                driver = await _dbContext.DispatchDrivers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.UserId == trip.DriverUserId.Value, cancellationToken);
            }

            var pickup = trip.Stops.FirstOrDefault(s => s.StopType == TripStopType.Pickup);
            var dropoff = trip.Stops.FirstOrDefault(s => s.StopType == TripStopType.Dropoff);

            decimal? pickupLat = pickup?.Latitude;
            decimal? pickupLon = pickup?.Longitude;
            if (!pickupLat.HasValue && !string.IsNullOrWhiteSpace(pickup?.LocationText) && _geocodingService != null)
            {
                var pGeo = await _geocodingService.GeocodeAddressAsync(pickup.LocationText, cancellationToken);
                if (pGeo != null)
                {
                    pickupLat = (decimal)pGeo.Latitude;
                    pickupLon = (decimal)pGeo.Longitude;
                }
            }

            decimal? dropoffLat = dropoff?.Latitude;
            decimal? dropoffLon = dropoff?.Longitude;
            if (!dropoffLat.HasValue && !string.IsNullOrWhiteSpace(dropoff?.LocationText) && _geocodingService != null)
            {
                var dGeo = await _geocodingService.GeocodeAddressAsync(dropoff.LocationText, cancellationToken);
                if (dGeo != null)
                {
                    dropoffLat = (decimal)dGeo.Latitude;
                    dropoffLon = (decimal)dGeo.Longitude;
                }
            }

            var now = DateTime.UtcNow;
            var latePickup = pickup?.ScheduledAt.HasValue == true && now > pickup.ScheduledAt.Value && trip.Status < TripStatus.AtPickup;
            var lateDelivery = dropoff?.ScheduledAt.HasValue == true && now > dropoff.ScheduledAt.Value && trip.Status < TripStatus.Delivered;

            response.Add(new LiveMapTripResponse(
                TripId: trip.Id,
                DispatchTruckId: truck?.Id,
                PlateNumber: truck?.PlateNumber ?? "Unknown",
                DispatchDriverId: driver?.Id,
                DriverName: trip.Driver?.Username ?? "Unknown",
                CurrentTripStatus: trip.Status.ToString().ToUpper(),
                LastLatitude: trip.LastLatitude,
                LastLongitude: trip.LastLongitude,
                LastLocationAt: trip.LastLocationAt,
                PickupLocation: pickup?.LocationText,
                PickupLatitude: pickupLat,
                PickupLongitude: pickupLon,
                PickupScheduledAt: pickup?.ScheduledAt,
                DropoffLocation: dropoff?.LocationText,
                DropoffLatitude: dropoffLat,
                DropoffLongitude: dropoffLon,
                DropoffScheduledAt: dropoff?.ScheduledAt,
                DelayFlag: latePickup || lateDelivery
            ));
        }

        return response;
    }

    public async Task<LiveMapTripResponse> GetDriverActiveRouteMapAsync(Guid driverUserId, CancellationToken cancellationToken = default)
    {
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

        var trip = await _dbContext.DispatchTrips
            .AsNoTracking()
            .Include(t => t.Driver)
            .Include(t => t.TruckAsset)
            .Include(t => t.Stops)
            .Where(t => t.DriverUserId == driverUserId && activeStatuses.Contains(t.Status))
            .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (trip == null)
        {
            throw new NotFoundException("No active trip found for this driver.");
        }

        Truck? truck = null;
        if (trip.TruckAssetId.HasValue)
        {
            truck = await _dbContext.DispatchTrucks
                .AsNoTracking()
                .FirstOrDefaultAsync(tr => tr.AssetId == trip.TruckAssetId.Value, cancellationToken);
        }

        Driver? driver = null;
        if (trip.DriverUserId.HasValue)
        {
            driver = await _dbContext.DispatchDrivers
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.UserId == trip.DriverUserId.Value, cancellationToken);
        }

        var pickup = trip.Stops.FirstOrDefault(s => s.StopType == TripStopType.Pickup);
        var dropoff = trip.Stops.FirstOrDefault(s => s.StopType == TripStopType.Dropoff);

        decimal? driverPickupLat = pickup?.Latitude;
        decimal? driverPickupLon = pickup?.Longitude;
        if (!driverPickupLat.HasValue && !string.IsNullOrWhiteSpace(pickup?.LocationText) && _geocodingService != null)
        {
            var pGeo = await _geocodingService.GeocodeAddressAsync(pickup.LocationText, cancellationToken);
            if (pGeo != null)
            {
                driverPickupLat = (decimal)pGeo.Latitude;
                driverPickupLon = (decimal)pGeo.Longitude;
            }
        }

        decimal? driverDropoffLat = dropoff?.Latitude;
        decimal? driverDropoffLon = dropoff?.Longitude;
        if (!driverDropoffLat.HasValue && !string.IsNullOrWhiteSpace(dropoff?.LocationText) && _geocodingService != null)
        {
            var dGeo = await _geocodingService.GeocodeAddressAsync(dropoff.LocationText, cancellationToken);
            if (dGeo != null)
            {
                driverDropoffLat = (decimal)dGeo.Latitude;
                driverDropoffLon = (decimal)dGeo.Longitude;
            }
        }

        var now = DateTime.UtcNow;
        var latePickup = pickup?.ScheduledAt.HasValue == true && now > pickup.ScheduledAt.Value && trip.Status < TripStatus.AtPickup;
        var lateDelivery = dropoff?.ScheduledAt.HasValue == true && now > dropoff.ScheduledAt.Value && trip.Status < TripStatus.Delivered;

        return new LiveMapTripResponse(
            TripId: trip.Id,
            DispatchTruckId: truck?.Id,
            PlateNumber: truck?.PlateNumber ?? "Unknown",
            DispatchDriverId: driver?.Id,
            DriverName: trip.Driver?.Username ?? "Unknown",
            CurrentTripStatus: trip.Status.ToString().ToUpper(),
            LastLatitude: trip.LastLatitude,
            LastLongitude: trip.LastLongitude,
            LastLocationAt: trip.LastLocationAt,
            PickupLocation: pickup?.LocationText,
            PickupLatitude: driverPickupLat,
            PickupLongitude: driverPickupLon,
            PickupScheduledAt: pickup?.ScheduledAt,
            DropoffLocation: dropoff?.LocationText,
            DropoffLatitude: driverDropoffLat,
            DropoffLongitude: driverDropoffLon,
            DropoffScheduledAt: dropoff?.ScheduledAt,
            DelayFlag: latePickup || lateDelivery
        );
    }

    private static void ValidateCoordinates(decimal latitude, decimal longitude)
    {
        if (latitude < -90 || latitude > 90)
        {
            throw new ArgumentException("Latitude must be between -90 and 90.");
        }
        if (longitude < -180 || longitude > 180)
        {
            throw new ArgumentException("Longitude must be between -180 and 180.");
        }
    }
}
