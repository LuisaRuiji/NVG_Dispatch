using NVGInventory.Modules.Dispatching.Enums;

namespace NVGInventory.Modules.Dispatching.Entities;

public sealed class TripStop
{
    public Guid Id { get; set; }
    public Guid TripId { get; set; }
    public Trip? Trip { get; set; }
    public TripStopType StopType { get; set; }
    public string LocationText { get; set; } = string.Empty;
    public DateTime? ScheduledAt { get; set; }
    public DateTime? ActualAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
