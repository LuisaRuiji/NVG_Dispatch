using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NVGInventory.Modules.Dispatching.Entities;
using NVGInventory.Modules.Dispatching.Enums;

namespace NVGInventory.Modules.DocumentManagement.Persistence.Configurations;

public sealed class TripDocumentConfiguration : IEntityTypeConfiguration<TripDocument>
{
    public void Configure(EntityTypeBuilder<TripDocument> entity)
    {
        var docTypeConverter = new ValueConverter<TripDocumentType, string>(
            value =>
                value == TripDocumentType.Waybill ? "WAYBILL" :
                value == TripDocumentType.Pod ? "POD" :
                value == TripDocumentType.Atw ? "ATW" :
                "WAYBILL",
            value =>
                value == "WAYBILL" ? TripDocumentType.Waybill :
                value == "POD" ? TripDocumentType.Pod :
                value == "ATW" ? TripDocumentType.Atw :
                TripDocumentType.Waybill);

        var docStateConverter = new ValueConverter<TripDocumentState, string>(
            value =>
                value == TripDocumentState.Missing ? "MISSING" :
                value == TripDocumentState.Uploaded ? "UPLOADED" :
                value == TripDocumentState.Verified ? "VERIFIED" :
                value == TripDocumentState.Rejected ? "REJECTED" :
                "MISSING",
            value =>
                value == "MISSING" ? TripDocumentState.Missing :
                value == "UPLOADED" ? TripDocumentState.Uploaded :
                value == "VERIFIED" ? TripDocumentState.Verified :
                value == "REJECTED" ? TripDocumentState.Rejected :
                TripDocumentState.Missing);

        entity.ToTable("dispatch_trip_documents");
        entity.HasKey(doc => doc.Id);
        entity.Property(doc => doc.Id).HasColumnName("id");
        entity.Property(doc => doc.TripId).HasColumnName("trip_id");
        entity.Property(doc => doc.Type)
            .HasColumnName("doc_type")
            .HasConversion(docTypeConverter)
            .HasMaxLength(20)
            .IsRequired();
        entity.Property(doc => doc.State)
            .HasColumnName("state")
            .HasConversion(docStateConverter)
            .HasMaxLength(20)
            .IsRequired();
        entity.Property(doc => doc.SupersedesDocumentId).HasColumnName("supersedes_document_id");
        entity.Property(doc => doc.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        entity.Property(doc => doc.StorageKey).HasColumnName("storage_key").HasMaxLength(500).IsRequired();
        entity.Property(doc => doc.UploadedByUserId).HasColumnName("uploaded_by_user_id");
        entity.Property(doc => doc.VerifiedByUserId).HasColumnName("verified_by_user_id");
        entity.Property(doc => doc.RejectedByUserId).HasColumnName("rejected_by_user_id");
        entity.Property(doc => doc.Remarks).HasColumnName("remarks").HasMaxLength(500);
        entity.Property(doc => doc.UploadedAt).HasColumnName("uploaded_at").HasDefaultValueSql("SYSUTCDATETIME()");
        entity.Property(doc => doc.VerifiedAt).HasColumnName("verified_at");
        entity.Property(doc => doc.RejectedAt).HasColumnName("rejected_at");
        entity.Property(doc => doc.RowVersion)
            .HasColumnName("row_version")
            .IsRowVersion();

        entity.HasOne(doc => doc.Trip)
            .WithMany(trip => trip.Documents)
            .HasForeignKey(doc => doc.TripId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(doc => doc.UploadedBy)
            .WithMany()
            .HasForeignKey(doc => doc.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(doc => doc.VerifiedBy)
            .WithMany()
            .HasForeignKey(doc => doc.VerifiedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(doc => doc.RejectedBy)
            .WithMany()
            .HasForeignKey(doc => doc.RejectedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(doc => doc.SupersedesDocument)
            .WithMany()
            .HasForeignKey(doc => doc.SupersedesDocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(doc => doc.TripId);
        entity.HasIndex(doc => new { doc.TripId, doc.Type, doc.IsActive });
        entity.HasIndex(doc => new { doc.TripId, doc.Type, doc.State });
        entity.HasIndex(doc => new { doc.TripId, doc.Type, doc.IsActive, doc.State });
    }
}
