namespace NVGInventory.Domain.Entities;

public sealed class Workflow
{
    public string WorkflowKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<WorkflowStep> Steps { get; set; } = new List<WorkflowStep>();
}
