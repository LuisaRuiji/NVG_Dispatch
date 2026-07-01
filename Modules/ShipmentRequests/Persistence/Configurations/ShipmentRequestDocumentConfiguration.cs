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
        entity.Property(doc => doc.UploadedByUserId).HasColumnName("uploaded_by_user_id");
        entity.Property(doc => doc.UploadedAt).HasColumnName("uploaded_at").HasDefaultValueSql("SYSUTCDATETIME()");

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
