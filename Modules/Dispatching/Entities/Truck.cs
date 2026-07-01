using NVGInventory.Domain.Entities;

namespace NVGInventory.Modules.Dispatching.Entities;

public sealed class Truck
{
    public Guid Id { get; set; }
    public Guid AssetId { get; set; }
    public Asset? Asset { get; set; }
    public string PlateNumber { get; set; } = string.Empty;
    public string ContainerCapability { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public DateTime CreatedAt { get; set; }
}
