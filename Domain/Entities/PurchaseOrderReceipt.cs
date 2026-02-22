namespace NVGInventory.Domain.Entities;

public sealed class PurchaseOrderReceipt
{
    public Guid Id { get; set; }
    public Guid PurchaseOrderLineId { get; set; }
    public decimal QtyReceivedIncrement { get; set; }
    public Guid ReceivedByUserId { get; set; }
    public DateTime ReceivedAt { get; set; }

    public PurchaseOrderLine? PurchaseOrderLine { get; set; }
    public User? ReceivedBy { get; set; }
}
