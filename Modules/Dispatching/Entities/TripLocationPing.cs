using NVGInventory.Domain.Entities;

namespace NVGInventory.Modules.Dispatching.Entities;

public sealed class TripLocationPing
{
    public Guid Id { get; set; }
    public Guid TripId { get; set; }
    public Trip? Trip { get; set; }
    public Guid DriverId { get; set; }
    public User? Driver { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? AccuracyMeters { get; set; }
    public DateTime RecordedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
