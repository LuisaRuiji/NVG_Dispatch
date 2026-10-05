using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NVGInventory.Modules.Dispatching.Entities;

namespace NVGInventory.Modules.Dispatching.Persistence.Configurations;

public sealed class TripOperationalEventConfiguration : IEntityTypeConfiguration<TripOperationalEvent>
{
    public void Configure(EntityTypeBuilder<TripOperationalEvent> entity)
    {
        entity.ToTable("dispatch_trip_operational_events");
        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).HasColumnName("id");
        entity.Property(item => item.TripId).HasColumnName("trip_id");
        entity.Property(item => item.EventType).HasColumnName("event_type").HasConversion<string>().HasMaxLength(50);
        entity.Property(item => item.ActorUserId).HasColumnName("actor_user_id");
        entity.Property(item => item.ActorRole).HasColumnName("actor_role").HasMaxLength(200).IsRequired();
        entity.Property(item => item.EventAt).HasColumnName("event_at");
        entity.Property(item => item.RecordedAt).HasColumnName("recorded_at").HasDefaultValueSql("SYSUTCDATETIME()");
        entity.Property(item => item.Location).HasColumnName("location").HasMaxLength(300);
        entity.Property(item => item.Reason).HasColumnName("reason").HasMaxLength(1000);
        entity.Property(item => item.ReferenceNumber).HasColumnName("reference_number").HasMaxLength(160);
        entity.Property(item => item.RelatedDocumentId).HasColumnName("related_document_id");
        entity.HasOne(item => item.Trip).WithMany(trip => trip.OperationalEvents).HasForeignKey(item => item.TripId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(item => item.Actor).WithMany().HasForeignKey(item => item.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.RelatedDocument).WithMany().HasForeignKey(item => item.RelatedDocumentId).OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(item => new { item.TripId, item.EventAt });
    }
}
