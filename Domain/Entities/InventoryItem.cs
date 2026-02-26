using NVGInventory.Domain.Enums;

namespace NVGInventory.Domain.Entities;

public sealed class InventoryItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public ItemType ItemType { get; set; }
    public bool IsKit { get; set; }
    public decimal Quantity { get; set; }
    public decimal AverageCost { get; set; }
    public decimal? LastCost { get; set; }
    public decimal? ReorderLevel { get; set; }
    public string? Location { get; set; }
    public decimal? UnitValue { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<RequestLine> RequestLines { get; set; } = new List<RequestLine>();
    public ICollection<StockLog> StockLogs { get; set; } = new List<StockLog>();
    public ICollection<KitComponent> KitComponents { get; set; } = new List<KitComponent>();
}
