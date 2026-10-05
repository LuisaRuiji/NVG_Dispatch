using NVGInventory.Domain.Entities;
using NVGInventory.Modules.Dispatching.Enums;

namespace NVGInventory.Modules.Dispatching.Entities;

public sealed class TripOperationalEvent
{
    public Guid Id { get; set; }
    public Guid TripId { get; set; }
    public Trip Trip { get; set; } = null!;
    public TripOperationalEventType EventType { get; set; }
    public Guid ActorUserId { get; set; }
    public User Actor { get; set; } = null!;
    public string ActorRole { get; set; } = string.Empty;
    public DateTime EventAt { get; set; }
    public DateTime RecordedAt { get; set; }
    public string? Location { get; set; }
    public string? Reason { get; set; }
    public string? ReferenceNumber { get; set; }
    public Guid? RelatedDocumentId { get; set; }
    public TripDocument? RelatedDocument { get; set; }
}
