using NVGInventory.Domain.Entities;
using NVGInventory.Modules.Dispatching.Entities;
using NVGInventory.Modules.ShipmentRequests.Enums;

namespace NVGInventory.Modules.ShipmentRequests.Entities;

public sealed class ShipmentRequest
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public ShipmentRequestStatus Status { get; set; }
    public string PickupLocation { get; set; } = string.Empty;
    public string DropoffLocation { get; set; } = string.Empty;
    public DateTime? RequestedPickupTime { get; set; }
    public string? CargoDescription { get; set; }
    public decimal? CargoWeight { get; set; }
    public string? SpecialInstructions { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public User? ApprovedByUser { get; set; }
    public Guid? ConvertedTripId { get; set; }
    public Trip? ConvertedTrip { get; set; }

    public List<ShipmentRequestDocument> Documents { get; set; } = [];
}
