using NVGInventory.Domain.Entities;

namespace NVGInventory.Modules.Dispatching.Entities;

public sealed class DispatchRecommendation
{
    public Guid Id { get; set; }
    public Guid CompletedTripId { get; set; }
    public Trip CompletedTrip { get; set; } = null!;
    public Guid DriverId { get; set; }
    public Guid TruckId { get; set; }
    public Guid RecommendedTripId { get; set; }
    public Trip RecommendedTrip { get; set; } = null!;
    public decimal ProximityScore { get; set; }
    public decimal AvailabilityScore { get; set; }
    public decimal TruckMatchScore { get; set; }
    public decimal AgingScore { get; set; }
    public decimal TotalScore { get; set; }
    public int Rank { get; set; }
    public DateTime GeneratedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool WasAccepted { get; set; }
    public bool WasIgnored { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public User? ReviewedByUser { get; set; }
    public DateTime? ReviewedAt { get; set; }
}
