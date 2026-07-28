using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NVGInventory.Modules.ShipmentRequests.Entities;
using NVGInventory.Modules.ShipmentRequests.Enums;

namespace NVGInventory.Modules.ShipmentRequests.Persistence.Configurations;

public sealed class ShipmentRequestDocumentConfiguration : IEntityTypeConfiguration<ShipmentRequestDocument>
{
    public void Configure(EntityTypeBuilder<ShipmentRequestDocument> entity)
    {
        var docTypeConverter = new ValueConverter<ShipmentRequestDocumentType, string>(
            value =>
                value == ShipmentRequestDocumentType.Atw ? "ATW" :
                value == ShipmentRequestDocumentType.Invoice ? "INVOICE" :
                value == ShipmentRequestDocumentType.CargoManifest ? "CARGO_MANIFEST" :
                value == ShipmentRequestDocumentType.DeliveryInstructions ? "DELIVERY_INSTRUCTIONS" :
                "OTHER",
            value =>
                value == "ATW" ? ShipmentRequestDocumentType.Atw :
                value == "INVOICE" ? ShipmentRequestDocumentType.Invoice :
                value == "CARGO_MANIFEST" ? ShipmentRequestDocumentType.CargoManifest :
                value == "DELIVERY_INSTRUCTIONS" ? ShipmentRequestDocumentType.DeliveryInstructions :
                ShipmentRequestDocumentType.Other);
        var analysisStatusConverter = new ValueConverter<DocumentAnalysisStatus, string>(
            value => value == DocumentAnalysisStatus.NeedsReview ? "NEEDS_REVIEW" :
                value == DocumentAnalysisStatus.Failed ? "FAILED" : "NOT_CONFIGURED",
            value => value == "NEEDS_REVIEW" ? DocumentAnalysisStatus.NeedsReview :
                value == "FAILED" ? DocumentAnalysisStatus.Failed : DocumentAnalysisStatus.NotConfigured);

        entity.ToTable("shipment_request_documents");
        entity.HasKey(doc => doc.Id);
        entity.Property(doc => doc.Id).HasColumnName("id");
        entity.Property(doc => doc.RequestId).HasColumnName("request_id");
        entity.Property(doc => doc.DocumentType)
            .HasColumnName("document_type")
            .HasConversion(docTypeConverter)
            .HasMaxLength(40)
            .IsRequired();
        entity.Property(doc => doc.StorageKey).HasColumnName("storage_key").HasMaxLength(500).IsRequired();
        entity.Property(doc => doc.OriginalFileName).HasColumnName("original_file_name").HasMaxLength(255);
        entity.Property(doc => doc.ContentType).HasColumnName("content_type").HasMaxLength(100);
        entity.Property(doc => doc.SizeBytes).HasColumnName("size_bytes");
        entity.Property(doc => doc.UploadedByUserId).HasColumnName("uploaded_by_user_id");
        entity.Property(doc => doc.UploadedAt).HasColumnName("uploaded_at").HasDefaultValueSql("SYSUTCDATETIME()");
        entity.Property(doc => doc.AnalysisStatus)
            .HasColumnName("analysis_status")
            .HasConversion(analysisStatusConverter)
            .HasMaxLength(20)
            .HasDefaultValue(DocumentAnalysisStatus.NotConfigured);
        entity.Property(doc => doc.AnalysisError).HasColumnName("analysis_error").HasMaxLength(500);
        entity.Property(doc => doc.ExtractedContainerNumber).HasColumnName("extracted_container_number").HasMaxLength(20);
        entity.Property(doc => doc.ExtractedBookingNumber).HasColumnName("extracted_booking_number").HasMaxLength(60);
        entity.Property(doc => doc.ExtractedShippingLine).HasColumnName("extracted_shipping_line").HasMaxLength(120);
        entity.Property(doc => doc.ExtractionConfidence).HasColumnName("extraction_confidence").HasColumnType("decimal(5,4)");
        entity.Property(doc => doc.RiskFlagsJson).HasColumnName("risk_flags_json").HasMaxLength(2000);
        entity.Property(doc => doc.AnalyzedAt).HasColumnName("analyzed_at");

        entity.HasOne(doc => doc.Request)
            .WithMany(request => request.Documents)
            .HasForeignKey(doc => doc.RequestId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(doc => doc.UploadedByUser)
            .WithMany()
            .HasForeignKey(doc => doc.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(doc => doc.RequestId);
        entity.HasIndex(doc => new { doc.RequestId, doc.DocumentType });
    }
}
