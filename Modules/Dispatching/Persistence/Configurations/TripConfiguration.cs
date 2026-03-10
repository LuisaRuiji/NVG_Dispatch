using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NVGInventory.Modules.Dispatching.Entities;
using NVGInventory.Modules.Dispatching.Enums;

namespace NVGInventory.Modules.Dispatching.Persistence.Configurations;

public sealed class TripConfiguration : IEntityTypeConfiguration<Trip>
{
    public void Configure(EntityTypeBuilder<Trip> entity)
    {
        var statusConverter = new ValueConverter<TripStatus, string>(
            value =>
                value == TripStatus.Draft ? "DRAFT" :
                value == TripStatus.Dispatched ? "DISPATCHED" :
                value == TripStatus.EnroutePickup ? "ENROUTE_PICKUP" :
                value == TripStatus.AtPickup ? "AT_PICKUP" :
                value == TripStatus.Loaded ? "LOADED" :
                value == TripStatus.EnrouteDropoff ? "ENROUTE_DROPOFF" :
                value == TripStatus.AtDropoff ? "AT_DROPOFF" :
                value == TripStatus.Delivered ? "DELIVERED" :
                value == TripStatus.Closed ? "CLOSED" :
                value == TripStatus.Cancelled ? "CANCELLED" :
                value == TripStatus.OnHold ? "ON_HOLD" :
                value == TripStatus.FailedAttempt ? "FAILED_ATTEMPT" :
                "DRAFT",
            value =>
                value == "DRAFT" ? TripStatus.Draft :
                value == "DISPATCHED" ? TripStatus.Dispatched :
                value == "ENROUTE_PICKUP" ? TripStatus.EnroutePickup :
                value == "AT_PICKUP" ? TripStatus.AtPickup :
                value == "LOADED" ? TripStatus.Loaded :
                value == "ENROUTE_DROPOFF" ? TripStatus.EnrouteDropoff :
                value == "AT_DROPOFF" ? TripStatus.AtDropoff :
                value == "DELIVERED" ? TripStatus.Delivered :
                value == "CLOSED" ? TripStatus.Closed :
                value == "CANCELLED" ? TripStatus.Cancelled :
                value == "ON_HOLD" ? TripStatus.OnHold :
                value == "FAILED_ATTEMPT" ? TripStatus.FailedAttempt :
                TripStatus.Draft);

        entity.ToTable("dispatch_trips");
        entity.HasKey(trip => trip.Id);
        entity.Property(trip => trip.Id).HasColumnName("id");
        entity.Property(trip => trip.CustomerId).HasColumnName("customer_id");
        entity.Property(trip => trip.DriverUserId).HasColumnName("driver_user_id");
        entity.Property(trip => trip.TruckAssetId).HasColumnName("truck_asset_id");
        entity.Property(trip => trip.Status)
            .HasColumnName("status")
            .HasConversion(statusConverter)
            .HasMaxLength(30)
            .IsRequired();
        entity.Property(trip => trip.PodPending).HasColumnName("pod_pending").HasDefaultValue(false).IsRequired();
        entity.Property(trip => trip.HoldPreviousStatus)
            .HasColumnName("hold_previous_status")
            .HasConversion(statusConverter)
            .HasMaxLength(30);
        entity.Property(trip => trip.Notes).HasColumnName("notes").HasMaxLength(400);
        entity.Property(trip => trip.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");
        entity.Property(trip => trip.UpdatedAt).HasColumnName("updated_at");
        entity.Property(trip => trip.RowVersion)
            .HasColumnName("row_version")
            .IsRowVersion();

        entity.HasOne(trip => trip.Customer)
            .WithMany(customer => customer.Trips)
            .HasForeignKey(trip => trip.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(trip => trip.Driver)
            .WithMany()
            .HasForeignKey(trip => trip.DriverUserId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(trip => trip.TruckAsset)
            .WithMany()
            .HasForeignKey(trip => trip.TruckAssetId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(trip => trip.Status);
        entity.HasIndex(trip => trip.DriverUserId);
        entity.HasIndex(trip => new { trip.DriverUserId, trip.Status });
        entity.HasIndex(trip => trip.CustomerId);
        entity.HasIndex(trip => trip.TruckAssetId);
        entity.HasIndex(trip => new { trip.TruckAssetId, trip.Status });
    }
}
