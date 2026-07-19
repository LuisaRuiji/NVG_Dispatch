using System;
using System.Collections.Generic;

namespace NVGInventory.Modules.Dispatching.Models;

public sealed class CandidateMove
{
    public Guid TripId { get; set; }
    public Guid DispatchTruckId { get; set; }
    public Guid DispatchDriverId { get; set; }
    public int InsertPosition { get; set; }
    public int AddedEmptyTravelMinutes { get; set; }
    public int AddedRouteMinutes { get; set; }
    public decimal LatenessRiskScore { get; set; }
    public decimal WorkloadImpactScore { get; set; }
    public decimal PriorityUrgencyScore { get; set; }
    public decimal MoveScore { get; set; }
    public bool CspPassed { get; set; }
    public IReadOnlyCollection<string> CspFailureReasons { get; set; } = Array.Empty<string>();
}
