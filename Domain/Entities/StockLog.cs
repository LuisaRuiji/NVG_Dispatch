using NVGInventory.Domain.Enums;

namespace NVGInventory.Domain.Entities;

public sealed class StockLog
{
    public Guid Id { get; set; }
    public StockMovementType MovementType { get; set; }
    public Guid InventoryId { get; set; }
    public decimal QtyDelta { get; set; }
    public decimal? UnitCostSnapshot { get; set; }
    public decimal? TotalCostSnapshot { get; set; }
    public string RefType { get; set; } = string.Empty;
    public Guid RefId { get; set; }
    public Guid ActorUserId { get; set; }
    public string? MetaJson { get; set; }
    public DateTime CreatedAt { get; set; }

    public InventoryItem? InventoryItem { get; set; }
    public User? Actor { get; set; }
}
