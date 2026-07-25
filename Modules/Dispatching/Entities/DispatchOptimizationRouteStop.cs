namespace NVGInventory.Modules.Dispatching.Entities;

public sealed class DispatchOptimizationRouteStop
{
    public Guid Id { get; set; }
    public Guid RouteId { get; set; }
    public DispatchOptimizationRoute? Route { get; set; }
    public Guid TripId { get; set; }
    public Trip? Trip { get; set; }
    public int StopOrder { get; set; }
    public DateTime? EstimatedArrivalAtPickup { get; set; }
    public DateTime? EstimatedArrivalAtDropoff { get; set; }
    public int EmptyTravelMinutesToPickup { get; set; }
    public int LoadedTravelMinutesToDropoff { get; set; }
    public decimal LatenessRiskScore { get; set; }
    public decimal PriorityUrgencyScore { get; set; }
    public decimal AssignmentScore { get; set; }
}
