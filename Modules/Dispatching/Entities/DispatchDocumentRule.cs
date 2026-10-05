using NVGInventory.Domain.Entities;
using NVGInventory.Modules.Dispatching.Enums;

namespace NVGInventory.Modules.Dispatching.Entities;

public sealed class DispatchDocumentRule
{
    public Guid Id { get; set; }
    public string TripType { get; set; } = string.Empty;
    public DispatchDocumentMilestone Milestone { get; set; }
    public string DocumentCode { get; set; } = string.Empty;
    public string Scope { get; set; } = "TRIP";
    public DocumentDirection Direction { get; set; } = DocumentDirection.NotApplicable;
    public string? AlternativeGroup { get; set; }
    public bool IsRequired { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
