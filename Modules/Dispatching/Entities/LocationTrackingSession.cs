using System;
using System.Collections.Generic;
using NVGInventory.Modules.Dispatching.Enums;

namespace NVGInventory.Modules.Dispatching.Entities;

public sealed class LocationTrackingSession
{
    public Guid Id { get; set; }
    public Guid TripId { get; set; }
    public Trip? Trip { get; set; }
    public Guid DispatchDriverId { get; set; }
    public Driver? DispatchDriver { get; set; }
    public Guid DispatchTruckId { get; set; }
    public Truck? DispatchTruck { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public TrackingSessionStatus Status { get; set; }
    public decimal? StartLatitude { get; set; }
    public decimal? StartLongitude { get; set; }
    public decimal? EndLatitude { get; set; }
    public decimal? EndLongitude { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<DriverLocationUpdate> LocationUpdates { get; set; } = [];
}
