namespace NVGInventory.Domain.Entities;

public sealed class PurchaseOrderLine
{
    public Guid Id { get; set; }
    public Guid PurchaseOrderId { get; set; }
    public Guid InventoryId { get; set; }
    public decimal QtyOrdered { get; set; }
    public decimal QtyReceived { get; set; }
    public decimal? UnitPrice { get; set; }
    public string? Remarks { get; set; }

    public PurchaseOrder? PurchaseOrder { get; set; }
    public InventoryItem? InventoryItem { get; set; }
    public ICollection<PurchaseOrderReceipt> Receipts { get; set; } = new List<PurchaseOrderReceipt>();
}
