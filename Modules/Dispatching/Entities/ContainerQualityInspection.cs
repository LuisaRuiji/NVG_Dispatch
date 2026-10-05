using NVGInventory.Domain.Entities;
using NVGInventory.Modules.Dispatching.Enums;

namespace NVGInventory.Modules.Dispatching.Entities;

public sealed class ContainerQualityInspection
{
    public Guid TripId { get; set; }
    public Trip Trip { get; set; } = null!;
    public bool HasDents { get; set; }
    public bool HasHoles { get; set; }
    public bool HasRust { get; set; }
    public bool HasOdor { get; set; }
    public bool HasResidue { get; set; }
    public bool HasStains { get; set; }
    public bool HasInsects { get; set; }
    public bool IsClean { get; set; }
    public bool FoodGradeRequired { get; set; }
    public bool FoodGradePassed { get; set; }
    public ContainerInspectionOutcome Outcome { get; set; } = ContainerInspectionOutcome.Pending;
    public string? Reason { get; set; }
    public Guid InspectedByUserId { get; set; }
    public User InspectedByUser { get; set; } = null!;
    public DateTime InspectedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public User? ReviewedByUser { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
