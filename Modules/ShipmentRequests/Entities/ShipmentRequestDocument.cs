using NVGInventory.Domain.Entities;
using NVGInventory.Modules.ShipmentRequests.Enums;

namespace NVGInventory.Modules.ShipmentRequests.Entities;

public sealed class ShipmentRequestDocument
{
    public Guid Id { get; set; }
    public Guid RequestId { get; set; }
    public ShipmentRequest? Request { get; set; }
    public ShipmentRequestDocumentType DocumentType { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public Guid UploadedByUserId { get; set; }
    public User? UploadedByUser { get; set; }
    public DateTime UploadedAt { get; set; }
}
