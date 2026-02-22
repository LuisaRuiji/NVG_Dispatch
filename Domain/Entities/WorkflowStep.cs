namespace NVGInventory.Domain.Entities;

public sealed class WorkflowStep
{
    public Guid Id { get; set; }
    public string WorkflowKey { get; set; } = string.Empty;
    public int StepOrder { get; set; }
    public string RequiredRole { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public Workflow? Workflow { get; set; }
}
