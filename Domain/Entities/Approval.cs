using NVGInventory.Domain.Enums;

namespace NVGInventory.Domain.Entities;

public sealed class Approval
{
    public Guid Id { get; set; }
    public string WorkflowKey { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public int CurrentStep { get; set; } = 1;
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public User? Creator { get; set; }
    public Workflow? Workflow { get; set; }
    public ICollection<ApprovalAction> Actions { get; set; } = new List<ApprovalAction>();
}
