using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NVGInventory.Modules.Dispatching.Entities;

namespace NVGInventory.Modules.Dispatching.Persistence.Configurations;

public sealed class TripLocationPingConfiguration : IEntityTypeConfiguration<TripLocationPing>
{
    public void Configure(EntityTypeBuilder<TripLocationPing> entity)
    {
        entity.ToTable("dispatch_trip_location_pings");
        entity.HasKey(ping => ping.Id);
        entity.Property(ping => ping.Id).HasColumnName("id");
        entity.Property(ping => ping.TripId).HasColumnName("trip_id");
        entity.Property(ping => ping.DriverId).HasColumnName("driver_id");
        entity.Property(ping => ping.Latitude).HasColumnName("latitude").IsRequired();
        entity.Property(ping => ping.Longitude).HasColumnName("longitude").IsRequired();
        entity.Property(ping => ping.AccuracyMeters).HasColumnName("accuracy_meters");
        entity.Property(ping => ping.RecordedAt).HasColumnName("recorded_at").IsRequired();
        entity.Property(ping => ping.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");

        entity.HasOne(ping => ping.Trip)
            .WithMany(trip => trip.LocationPings)
            .HasForeignKey(ping => ping.TripId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(ping => ping.Driver)
            .WithMany()
            .HasForeignKey(ping => ping.DriverId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(ping => new { ping.TripId, ping.RecordedAt }).IsDescending(false, true);
        entity.HasIndex(ping => new { ping.DriverId, ping.RecordedAt }).IsDescending(false, true);
    }
}
