using NVGInventory.Domain.Entities;
using NVGInventory.Modules.Dispatching.Enums;

namespace NVGInventory.Modules.Dispatching.Entities;

public sealed class TripDocument
{
    public Guid Id { get; set; }
    public Guid TripId { get; set; }
    public Trip? Trip { get; set; }
    public TripDocumentType Type { get; set; }
    public TripDocumentState State { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public Guid UploadedByUserId { get; set; }
    public User? UploadedBy { get; set; }
    public Guid? VerifiedByUserId { get; set; }
    public User? VerifiedBy { get; set; }
    public Guid? RejectedByUserId { get; set; }
    public User? RejectedBy { get; set; }
    public string? Remarks { get; set; }
    public DateTime UploadedAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
}
