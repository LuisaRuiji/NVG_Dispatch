using NVGInventory.Domain.Entities;

namespace NVGInventory.Modules.Dispatching.Entities;

public sealed class OptimizationWeightSettings
{
    public Guid Id { get; set; }
    public decimal DeadheadDistanceWeight { get; set; }
    public decimal CleaningTimeWeight { get; set; }
    public decimal WaitingTimeWeight { get; set; }
    public decimal JobUrgencyWeight { get; set; }
    public decimal CargoCompatibilityWeight { get; set; }
    public decimal AssetUtilizationWeight { get; set; }
    public bool IsActive { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public DateTime CreatedAt { get; set; }
}
