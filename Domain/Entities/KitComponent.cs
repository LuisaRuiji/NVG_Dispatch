namespace NVGInventory.Domain.Entities;

public sealed class KitComponent
{
    public Guid Id { get; set; }
    public Guid InventoryItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal RequiredQty { get; set; }
    public bool IsRequired { get; set; } = true;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }

    public InventoryItem? InventoryItem { get; set; }
}
