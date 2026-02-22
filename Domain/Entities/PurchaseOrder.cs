using NVGInventory.Domain.Enums;

namespace NVGInventory.Domain.Entities;

public sealed class PurchaseOrder
{
    public Guid Id { get; set; }
    public Guid SupplierId { get; set; }
    public string? Notes { get; set; }
    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public User? CreatedBy { get; set; }
    public Supplier? Supplier { get; set; }
    public ICollection<PurchaseOrderLine> Lines { get; set; } = new List<PurchaseOrderLine>();
}
