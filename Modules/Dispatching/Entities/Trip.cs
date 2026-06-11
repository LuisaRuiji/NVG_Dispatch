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
    public decimal? Rate { get; set; }
    public decimal? Payroll { get; set; }
    public decimal? Allowance { get; set; }
    public decimal? FuelAmount { get; set; }
    public decimal? FuelPricePerLiter { get; set; }
    public string? OfficialReceiptNumber { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public List<TripStop> Stops { get; set; } = [];
    public List<TripStatusHistory> StatusHistory { get; set; } = [];
    public List<TripDocument> Documents { get; set; } = [];
}
