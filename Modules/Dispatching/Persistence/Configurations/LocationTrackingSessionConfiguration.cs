using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NVGInventory.Modules.Dispatching.Entities;

namespace NVGInventory.Modules.Dispatching.Persistence.Configurations;

public sealed class LocationTrackingSessionConfiguration : IEntityTypeConfiguration<LocationTrackingSession>
{
    public void Configure(EntityTypeBuilder<LocationTrackingSession> entity)
    {
        entity.ToTable("location_tracking_sessions");
        entity.HasKey(s => s.Id);
        entity.Property(s => s.Id).HasColumnName("id");
        entity.Property(s => s.TripId).HasColumnName("trip_id").IsRequired();
        entity.Property(s => s.DispatchDriverId).HasColumnName("dispatch_driver_id").IsRequired();
        entity.Property(s => s.DispatchTruckId).HasColumnName("dispatch_truck_id").IsRequired();
        entity.Property(s => s.StartedAt).HasColumnName("started_at").IsRequired();
        entity.Property(s => s.EndedAt).HasColumnName("ended_at");
        entity.Property(s => s.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();
        entity.Property(s => s.StartLatitude)
            .HasColumnName("start_latitude")
            .HasColumnType("decimal(9,6)");
        entity.Property(s => s.StartLongitude)
            .HasColumnName("start_longitude")
            .HasColumnType("decimal(9,6)");
        entity.Property(s => s.EndLatitude)
            .HasColumnName("end_latitude")
            .HasColumnType("decimal(9,6)");
        entity.Property(s => s.EndLongitude)
            .HasColumnName("end_longitude")
            .HasColumnType("decimal(9,6)");
        entity.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();
        entity.Property(s => s.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        // Relationships
        entity.HasOne(s => s.Trip)
            .WithMany()
            .HasForeignKey(s => s.TripId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(s => s.DispatchDriver)
            .WithMany()
            .HasForeignKey(s => s.DispatchDriverId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(s => s.DispatchTruck)
            .WithMany()
            .HasForeignKey(s => s.DispatchTruckId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        entity.HasIndex(s => new { s.TripId, s.Status });
        entity.HasIndex(s => new { s.DispatchDriverId, s.Status });
    }
}
