using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NVGInventory.Modules.ShipmentRequests.Entities;
using NVGInventory.Modules.ShipmentRequests.Enums;

namespace NVGInventory.Modules.ShipmentRequests.Persistence.Configurations;

public sealed class ShipmentRequestConfiguration : IEntityTypeConfiguration<ShipmentRequest>
{
    public void Configure(EntityTypeBuilder<ShipmentRequest> entity)
    {
        var statusConverter = new ValueConverter<ShipmentRequestStatus, string>(
            value =>
                value == ShipmentRequestStatus.Draft ? "DRAFT" :
                value == ShipmentRequestStatus.Submitted ? "SUBMITTED" :
                value == ShipmentRequestStatus.Approved ? "APPROVED" :
                value == ShipmentRequestStatus.Rejected ? "REJECTED" :
                "CONVERTED_TO_TRIP",
            value =>
                value == "DRAFT" ? ShipmentRequestStatus.Draft :
                value == "SUBMITTED" ? ShipmentRequestStatus.Submitted :
                value == "APPROVED" ? ShipmentRequestStatus.Approved :
                value == "REJECTED" ? ShipmentRequestStatus.Rejected :
                ShipmentRequestStatus.ConvertedToTrip);

        entity.ToTable("shipment_requests");
        entity.HasKey(request => request.Id);
        entity.Property(request => request.Id).HasColumnName("id");
        entity.Property(request => request.CustomerId).HasColumnName("customer_id");
        entity.Property(request => request.Status)
            .HasColumnName("status")
            .HasConversion(statusConverter)
            .HasMaxLength(30)
            .IsRequired();
        entity.Property(request => request.PickupLocation).HasColumnName("pickup_location").HasMaxLength(300).IsRequired();
        entity.Property(request => request.DropoffLocation).HasColumnName("dropoff_location").HasMaxLength(300).IsRequired();
        entity.Property(request => request.RequestedPickupTime).HasColumnName("requested_pickup_time");
        entity.Property(request => request.CargoDescription).HasColumnName("cargo_description").HasMaxLength(500);
        entity.Property(request => request.CargoWeight).HasColumnName("cargo_weight").HasColumnType("decimal(18,3)");
        entity.Property(request => request.SpecialInstructions).HasColumnName("special_instructions").HasMaxLength(600);
        entity.Property(request => request.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");
        entity.Property(request => request.CreatedByUserId).HasColumnName("created_by_user_id");
        entity.Property(request => request.ApprovedAt).HasColumnName("approved_at");
        entity.Property(request => request.ApprovedByUserId).HasColumnName("approved_by_user_id");
        entity.Property(request => request.ConvertedTripId).HasColumnName("converted_trip_id");

        entity.HasOne(request => request.Customer)
            .WithMany()
            .HasForeignKey(request => request.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(request => request.CreatedByUser)
            .WithMany()
            .HasForeignKey(request => request.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(request => request.ApprovedByUser)
            .WithMany()
            .HasForeignKey(request => request.ApprovedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(request => request.ConvertedTrip)
            .WithMany()
            .HasForeignKey(request => request.ConvertedTripId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(request => request.CustomerId);
        entity.HasIndex(request => request.Status);
        entity.HasIndex(request => new { request.CustomerId, request.Status });
    }
}
