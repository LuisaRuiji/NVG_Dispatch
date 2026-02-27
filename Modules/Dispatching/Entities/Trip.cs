using NVGInventory.Domain.Entities;
using NVGInventory.Modules.Dispatching.Enums;

namespace NVGInventory.Modules.Dispatching.Entities;

public sealed class Trip
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public Guid? DriverUserId { get; set; }
    public User? Driver { get; set; }
    public Guid? TruckAssetId { get; set; }
    public Asset? TruckAsset { get; set; }
    public TripStatus Status { get; set; }
    public bool PodPending { get; set; }
    public TripStatus? HoldPreviousStatus { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public List<TripStop> Stops { get; set; } = [];
    public List<TripStatusHistory> StatusHistory { get; set; } = [];
    public List<TripDocument> Documents { get; set; } = [];
}
