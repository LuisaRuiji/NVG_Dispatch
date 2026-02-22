using NVGInventory.Domain.Enums;

namespace NVGInventory.Domain.Entities;

public sealed class InventoryAdjustment
{
    public Guid Id { get; set; }
    public InventoryAdjustmentStatus Status { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public User? CreatedBy { get; set; }
    public ICollection<InventoryAdjustmentLine> Lines { get; set; } = new List<InventoryAdjustmentLine>();
}
