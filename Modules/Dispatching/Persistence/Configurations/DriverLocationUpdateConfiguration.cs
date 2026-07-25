using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NVGInventory.Modules.Dispatching.Entities;

namespace NVGInventory.Modules.Dispatching.Persistence.Configurations;

public sealed class DriverLocationUpdateConfiguration : IEntityTypeConfiguration<DriverLocationUpdate>
{
    public void Configure(EntityTypeBuilder<DriverLocationUpdate> entity)
    {
        entity.ToTable("driver_location_updates");
        entity.HasKey(u => u.Id);
        entity.Property(u => u.Id).HasColumnName("id");
        entity.Property(u => u.TrackingSessionId).HasColumnName("tracking_session_id").IsRequired();
        entity.Property(u => u.TripId).HasColumnName("trip_id").IsRequired();
        entity.Property(u => u.DispatchDriverId).HasColumnName("dispatch_driver_id").IsRequired();
        entity.Property(u => u.DispatchTruckId).HasColumnName("dispatch_truck_id").IsRequired();
        entity.Property(u => u.Latitude)
            .HasColumnName("latitude")
            .HasColumnType("decimal(9,6)")
            .IsRequired();
        entity.Property(u => u.Longitude)
            .HasColumnName("longitude")
            .HasColumnType("decimal(9,6)")
            .IsRequired();
        entity.Property(u => u.AccuracyMeters)
            .HasColumnName("accuracy_meters")
            .HasColumnType("decimal(9,2)");
        entity.Property(u => u.SpeedKph)
            .HasColumnName("speed_kph")
            .HasColumnType("decimal(9,2)");
        entity.Property(u => u.Heading)
            .HasColumnName("heading")
            .HasColumnType("decimal(9,2)");
        entity.Property(u => u.RecordedAt).HasColumnName("recorded_at").IsRequired();
        entity.Property(u => u.ReceivedAt).HasColumnName("received_at").IsRequired();
        entity.Property(u => u.Source)
            .HasColumnName("source")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        // Relationships
        entity.HasOne(u => u.TrackingSession)
            .WithMany(s => s.LocationUpdates)
            .HasForeignKey(u => u.TrackingSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(u => u.Trip)
            .WithMany()
            .HasForeignKey(u => u.TripId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(u => u.DispatchDriver)
            .WithMany()
            .HasForeignKey(u => u.DispatchDriverId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(u => u.DispatchTruck)
            .WithMany()
            .HasForeignKey(u => u.DispatchTruckId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        entity.HasIndex(u => new { u.TripId, u.RecordedAt });
        entity.HasIndex(u => new { u.DispatchDriverId, u.RecordedAt });
        entity.HasIndex(u => new { u.DispatchTruckId, u.RecordedAt });
    }
}
