namespace NVGInventory.Modules.Dispatching.Entities;

public sealed class DispatchOptimizationRoute
{
    public Guid Id { get; set; }
    public Guid PlanId { get; set; }
    public DispatchOptimizationPlan? Plan { get; set; }
    public Guid DispatchTruckId { get; set; }
    public Truck? DispatchTruck { get; set; }
    public Guid DispatchDriverId { get; set; }
    public Driver? DispatchDriver { get; set; }
    public int SequenceNumber { get; set; }
    public decimal EstimatedDistanceKm { get; set; }
    public decimal EstimatedEmptyMileageKm { get; set; }
    public int EstimatedTravelMinutes { get; set; }
    public decimal WorkloadScore { get; set; }
    public decimal RouteCost { get; set; }

    public ICollection<DispatchOptimizationRouteStop> RouteStops { get; set; } = new List<DispatchOptimizationRouteStop>();
}
