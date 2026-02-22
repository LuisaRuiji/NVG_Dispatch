namespace NVGInventory.Domain.Entities;

public sealed class InventoryAdjustmentLine
{
    public Guid Id { get; set; }
    public Guid InventoryAdjustmentId { get; set; }
    public Guid InventoryId { get; set; }
    public decimal QtyDelta { get; set; }
    public string? Remarks { get; set; }
    public DateTime CreatedAt { get; set; }

    public InventoryAdjustment? InventoryAdjustment { get; set; }
    public InventoryItem? InventoryItem { get; set; }
}
