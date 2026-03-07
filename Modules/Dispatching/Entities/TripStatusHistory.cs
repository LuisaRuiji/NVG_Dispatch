using NVGInventory.Domain.Entities;
using NVGInventory.Modules.Dispatching.Enums;

namespace NVGInventory.Modules.Dispatching.Entities;

public sealed class TripStatusHistory
{
    public Guid Id { get; set; }
    public Guid TripId { get; set; }
    public Trip? Trip { get; set; }
    public TripHistoryEventType EventType { get; set; } = TripHistoryEventType.StatusChange;
    public TripStatus FromStatus { get; set; }
    public TripStatus ToStatus { get; set; }
    public Guid ActorUserId { get; set; }
    public User? Actor { get; set; }
    public string? Remarks { get; set; }
    public DateTime EventAt { get; set; }
    public DateTime RecordedAt { get; set; }
}
