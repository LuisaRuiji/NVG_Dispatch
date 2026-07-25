using System;
using System.Collections.Generic;
using System.Linq;

namespace NVGInventory.Modules.Dispatching.Models;

public sealed class DispatchSearchState
{
    public List<Guid> AssignedTripIds { get; set; } = new();
    public List<Guid> UnassignedTripIds { get; set; } = new();
    public Dictionary<Guid, DispatchSearchRoute> RoutesByTruck { get; set; } = new();
    public Dictionary<Guid, (decimal Lat, decimal Lon)> CurrentLocationByTruck { get; set; } = new();
    public int EstimatedTotalTravelMinutes { get; set; }
    public decimal EstimatedTotalEmptyMileageKm { get; set; }
    public decimal EstimatedTotalDistanceKm { get; set; }
    public decimal EstimatedLatenessRisk { get; set; }
    public decimal WorkloadBalanceScore { get; set; }
    public decimal PriorityUrgencyScore { get; set; }
    public decimal StateCost { get; set; }
    public int Depth { get; set; }

    public DispatchSearchState Clone()
    {
        var cloned = new DispatchSearchState
        {
            AssignedTripIds = new List<Guid>(AssignedTripIds),
            UnassignedTripIds = new List<Guid>(UnassignedTripIds),
            CurrentLocationByTruck = new Dictionary<Guid, (decimal Lat, decimal Lon)>(CurrentLocationByTruck),
            EstimatedTotalTravelMinutes = EstimatedTotalTravelMinutes,
            EstimatedTotalEmptyMileageKm = EstimatedTotalEmptyMileageKm,
            EstimatedTotalDistanceKm = EstimatedTotalDistanceKm,
            EstimatedLatenessRisk = EstimatedLatenessRisk,
            WorkloadBalanceScore = WorkloadBalanceScore,
            PriorityUrgencyScore = PriorityUrgencyScore,
            StateCost = StateCost,
            Depth = Depth
        };

        foreach (var kvp in RoutesByTruck)
        {
            cloned.RoutesByTruck[kvp.Key] = kvp.Value.Clone();
        }

        return cloned;
    }
}
