using System;
using NVGInventory.Modules.Dispatching.Enums;

namespace NVGInventory.Modules.Dispatching.Entities;

public sealed class DriverLocationUpdate
{
    public Guid Id { get; set; }
    public Guid TrackingSessionId { get; set; }
    public LocationTrackingSession? TrackingSession { get; set; }
    public Guid TripId { get; set; }
    public Trip? Trip { get; set; }
    public Guid DispatchDriverId { get; set; }
    public Driver? DispatchDriver { get; set; }
    public Guid DispatchTruckId { get; set; }
    public Truck? DispatchTruck { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public decimal? AccuracyMeters { get; set; }
    public decimal? SpeedKph { get; set; }
    public decimal? Heading { get; set; }
    public DateTime RecordedAt { get; set; }
    public DateTime ReceivedAt { get; set; }
    public LocationUpdateSource Source { get; set; }
}
