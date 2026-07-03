using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NVGInventory.Modules.Dispatching.Entities;
using NVGInventory.Modules.Dispatching.Enums;

namespace NVGInventory.Modules.Dispatching.Persistence.Configurations;

public sealed class TripStopConfiguration : IEntityTypeConfiguration<TripStop>
{
    public void Configure(EntityTypeBuilder<TripStop> entity)
    {
        var stopTypeConverter = new ValueConverter<TripStopType, string>(
            value => value == TripStopType.Pickup ? "PICKUP" : "DROPOFF",
            value => value == "PICKUP" ? TripStopType.Pickup : TripStopType.Dropoff);

        entity.ToTable("dispatch_trip_stops");
        entity.HasKey(stop => stop.Id);
        entity.Property(stop => stop.Id).HasColumnName("id");
        entity.Property(stop => stop.TripId).HasColumnName("trip_id");
        entity.Property(stop => stop.StopType)
            .HasColumnName("stop_type")
            .HasConversion(stopTypeConverter)
            .HasMaxLength(20)
            .IsRequired();
        entity.Property(stop => stop.LocationText).HasColumnName("location_text").HasMaxLength(300).IsRequired();
        entity.Property(stop => stop.Latitude).HasColumnName("latitude");
        entity.Property(stop => stop.Longitude).HasColumnName("longitude");
        entity.Property(stop => stop.ScheduledAt).HasColumnName("scheduled_at");
        entity.Property(stop => stop.ActualAt).HasColumnName("actual_at");
        entity.Property(stop => stop.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");

        entity.HasOne(stop => stop.Trip)
            .WithMany(trip => trip.Stops)
            .HasForeignKey(stop => stop.TripId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(stop => stop.TripId);
        entity.HasIndex(stop => new { stop.TripId, stop.StopType, stop.ScheduledAt });
        entity.HasIndex(stop => new { stop.StopType, stop.ScheduledAt });
    }
}
