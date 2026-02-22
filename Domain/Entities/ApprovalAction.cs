using NVGInventory.Domain.Enums;

namespace NVGInventory.Domain.Entities;

public sealed class ApprovalAction
{
    public Guid Id { get; set; }
    public Guid ApprovalId { get; set; }
    public int StepOrder { get; set; }
    public Guid ActorUserId { get; set; }
    public ApprovalDecision Decision { get; set; }
    public string? Remarks { get; set; }
    public DateTime ActedAt { get; set; }

    public Approval? Approval { get; set; }
    public User? Actor { get; set; }
}
