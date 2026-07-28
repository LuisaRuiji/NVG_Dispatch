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
    public string? OriginalFileName { get; set; }
    public string? ContentType { get; set; }
    public long? SizeBytes { get; set; }
    public Guid UploadedByUserId { get; set; }
    public User? UploadedByUser { get; set; }
    public DateTime UploadedAt { get; set; }
    public DocumentAnalysisStatus AnalysisStatus { get; set; } = DocumentAnalysisStatus.NotConfigured;
    public string? AnalysisError { get; set; }
    public string? ExtractedContainerNumber { get; set; }
    public string? ExtractedBookingNumber { get; set; }
    public string? ExtractedShippingLine { get; set; }
    public decimal? ExtractionConfidence { get; set; }
    public string? RiskFlagsJson { get; set; }
    public DateTime? AnalyzedAt { get; set; }
}
