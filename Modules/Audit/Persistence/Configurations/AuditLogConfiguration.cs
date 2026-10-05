using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NVGInventory.Domain.Entities;

namespace NVGInventory.Modules.Audit.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> entity)
    {
        entity.ToTable("audit_logs");
        entity.HasKey(log => log.Id);
        entity.Property(log => log.Id).HasColumnName("id");
        entity.Property(log => log.ActorUserId).HasColumnName("actor_user_id");
        entity.Property(log => log.ActorRole).HasColumnName("actor_role").HasMaxLength(200);
        entity.Property(log => log.TripId).HasColumnName("trip_id");
        entity.Property(log => log.Action).HasColumnName("action").HasMaxLength(120).IsRequired();
        entity.Property(log => log.EntityType).HasColumnName("entity_type").HasMaxLength(50).IsRequired();
        entity.Property(log => log.EntityId).HasColumnName("entity_id");
        entity.Property(log => log.BeforeJson).HasColumnName("before_json");
        entity.Property(log => log.AfterJson).HasColumnName("after_json");
        entity.Property(log => log.TraceId).HasColumnName("trace_id").HasMaxLength(64);
        entity.Property(log => log.Reason).HasColumnName("reason").HasMaxLength(1000);
        entity.Property(log => log.RelatedAttachmentId).HasColumnName("related_attachment_id");
        entity.Property(log => log.ReferenceNumber).HasColumnName("reference_number").HasMaxLength(160);
        entity.Property(log => log.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");

        entity.HasOne(log => log.Actor)
            .WithMany()
            .HasForeignKey(log => log.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(log => log.TripId);
        entity.HasIndex(log => log.CreatedAt)
            .IsDescending();
        entity.HasIndex(log => new { log.Action, log.CreatedAt })
            .IsDescending(false, true);
        entity.HasIndex(log => new { log.ActorUserId, log.CreatedAt })
            .IsDescending(false, true);
        entity.HasIndex(log => new { log.EntityType, log.CreatedAt })
            .IsDescending(false, true);
        entity.HasIndex(log => new { log.EntityType, log.EntityId, log.CreatedAt })
            .IsDescending(false, false, true);
    }
}
