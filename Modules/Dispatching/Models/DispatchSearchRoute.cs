using System;
using System.Collections.Generic;

namespace NVGInventory.Modules.Dispatching.Models;

public sealed class DispatchSearchRoute
{
    public Guid DispatchTruckId { get; set; }
    public Guid DispatchDriverId { get; set; }
    public List<Guid> OrderedTripIds { get; set; } = new();
    public decimal CurrentLatitude { get; set; }
    public decimal CurrentLongitude { get; set; }
    public int EstimatedTravelMinutes { get; set; }
    public decimal EstimatedEmptyMileageKm { get; set; }
    public decimal EstimatedDistanceKm { get; set; }
    public decimal WorkloadScore { get; set; }
    public decimal RouteCost { get; set; }

    public DispatchSearchRoute Clone()
    {
        return new DispatchSearchRoute
        {
            DispatchTruckId = DispatchTruckId,
            DispatchDriverId = DispatchDriverId,
            OrderedTripIds = new List<Guid>(OrderedTripIds),
            CurrentLatitude = CurrentLatitude,
            CurrentLongitude = CurrentLongitude,
            EstimatedTravelMinutes = EstimatedTravelMinutes,
            EstimatedEmptyMileageKm = EstimatedEmptyMileageKm,
            EstimatedDistanceKm = EstimatedDistanceKm,
            WorkloadScore = WorkloadScore,
            RouteCost = RouteCost
        };
    }
}
