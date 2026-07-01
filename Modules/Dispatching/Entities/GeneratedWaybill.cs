using NVGInventory.Domain.Entities;

namespace NVGInventory.Modules.Dispatching.Entities;

public sealed class GeneratedWaybill
{
    public Guid Id { get; set; }
    public Guid TripId { get; set; }
    public Trip? Trip { get; set; }
    public string WaybillNumber { get; set; } = string.Empty;
    public int Version { get; set; }
    public DateTime GeneratedAt { get; set; }
    public Guid GeneratedByUserId { get; set; }
    public User? GeneratedByUser { get; set; }
    public string WaybillDataJson { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
