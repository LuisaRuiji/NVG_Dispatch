using NVGInventory.Domain.Entities;
using NVGInventory.Modules.Dispatching.Enums;

namespace NVGInventory.Modules.Dispatching.Entities;

public sealed class DispatchOptimizationPlan
{
    public Guid Id { get; set; }
    public DateTime ScheduledDate { get; set; }
    public Guid? GeneratedByUserId { get; set; }
    public User? GeneratedByUser { get; set; }
    public OptimizationPlanStatus Status { get; set; }
    public string AlgorithmName { get; set; } = string.Empty;
    public bool IsCompleteSolution { get; set; }
    public int SearchIterations { get; set; }
    public int MaxIterations { get; set; }
    public int MaxCandidatesPerState { get; set; }
    public int MaxExecutionSeconds { get; set; }
    public decimal TotalEstimatedDistanceKm { get; set; }
    public decimal TotalEstimatedEmptyMileageKm { get; set; }
    public int TotalEstimatedTravelMinutes { get; set; }
    public decimal TotalLatenessRisk { get; set; }
    public decimal FinalStateCost { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public User? ApprovedByUser { get; set; }

    public ICollection<DispatchOptimizationRoute> Routes { get; set; } = new List<DispatchOptimizationRoute>();
}
