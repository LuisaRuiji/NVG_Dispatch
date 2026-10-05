using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NVGInventory.Modules.Dispatching.Entities;

namespace NVGInventory.Modules.Dispatching.Persistence.Configurations;

/// <summary>Maps immutable trip-receipt snapshots and their charge lines.</summary>
public sealed class TripReceiptConfiguration : IEntityTypeConfiguration<TripReceipt>
{
    public void Configure(EntityTypeBuilder<TripReceipt> entity)
    {
        entity.ToTable("dispatch_trip_receipts");
        entity.HasKey(receipt => receipt.Id);
        entity.Property(receipt => receipt.Id).HasColumnName("id");
        entity.Property(receipt => receipt.TripId).HasColumnName("trip_id");
        entity.Property(receipt => receipt.CustomerId).HasColumnName("customer_id");
        entity.Property(receipt => receipt.CustomerName).HasColumnName("customer_name").HasMaxLength(200).IsRequired();
        entity.Property(receipt => receipt.ReceiptNumber).HasColumnName("receipt_number").HasMaxLength(80).IsRequired();
        entity.Property(receipt => receipt.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        entity.Property(receipt => receipt.ContainerNumber).HasColumnName("container_number").HasMaxLength(20);
        entity.Property(receipt => receipt.BookingNumber).HasColumnName("booking_number").HasMaxLength(30);
        entity.Property(receipt => receipt.PickupLocation).HasColumnName("pickup_location").HasMaxLength(300);
        entity.Property(receipt => receipt.DropoffLocation).HasColumnName("dropoff_location").HasMaxLength(300);
        entity.Property(receipt => receipt.BaseCharge).HasColumnName("base_charge").HasColumnType("decimal(18,2)");
        entity.Property(receipt => receipt.Subtotal).HasColumnName("subtotal").HasColumnType("decimal(18,2)");
        entity.Property(receipt => receipt.DiscountAmount).HasColumnName("discount_amount").HasColumnType("decimal(18,2)");
        entity.Property(receipt => receipt.DiscountPercent).HasColumnName("discount_percent").HasColumnType("decimal(9,2)");
        entity.Property(receipt => receipt.DiscountReason).HasColumnName("discount_reason").HasMaxLength(300);
        entity.Property(receipt => receipt.TaxAmount).HasColumnName("tax_amount").HasColumnType("decimal(18,2)");
        entity.Property(receipt => receipt.Total).HasColumnName("total").HasColumnType("decimal(18,2)");
        entity.Property(receipt => receipt.PaymentMethod).HasColumnName("payment_method").HasMaxLength(80);
        entity.Property(receipt => receipt.PaymentReference).HasColumnName("payment_reference").HasMaxLength(120);
        entity.Property(receipt => receipt.Notes).HasColumnName("notes").HasMaxLength(600);
        entity.Property(receipt => receipt.IsReversal).HasColumnName("is_reversal").HasDefaultValue(false);
        entity.Property(receipt => receipt.ReversesReceiptId).HasColumnName("reverses_receipt_id");
        entity.Property(receipt => receipt.CorrectionReason).HasColumnName("correction_reason").HasMaxLength(600);
        entity.Property(receipt => receipt.GeneratedByUserId).HasColumnName("generated_by_user_id");
        entity.Property(receipt => receipt.GeneratedAt).HasColumnName("generated_at").HasDefaultValueSql("SYSUTCDATETIME()");
        entity.HasOne(receipt => receipt.Trip).WithMany(trip => trip.Receipts).HasForeignKey(receipt => receipt.TripId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(receipt => receipt.GeneratedByUser).WithMany().HasForeignKey(receipt => receipt.GeneratedByUserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(receipt => receipt.ReversesReceipt).WithMany().HasForeignKey(receipt => receipt.ReversesReceiptId).OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(receipt => receipt.ReceiptNumber).IsUnique();
        entity.HasIndex(receipt => new { receipt.TripId, receipt.GeneratedAt });
    }
}

/// <summary>Maps the immutable detail rows retained with each receipt snapshot.</summary>
public sealed class TripReceiptChargeConfiguration : IEntityTypeConfiguration<TripReceiptCharge>
{
    public void Configure(EntityTypeBuilder<TripReceiptCharge> entity)
    {
        entity.ToTable("dispatch_trip_receipt_charges");
        entity.HasKey(charge => charge.Id);
        entity.Property(charge => charge.Id).HasColumnName("id");
        entity.Property(charge => charge.TripReceiptId).HasColumnName("trip_receipt_id");
        entity.Property(charge => charge.Description).HasColumnName("description").HasMaxLength(120).IsRequired();
        entity.Property(charge => charge.Amount).HasColumnName("amount").HasColumnType("decimal(18,2)");
        entity.HasOne(charge => charge.TripReceipt).WithMany(receipt => receipt.AdditionalCharges).HasForeignKey(charge => charge.TripReceiptId).OnDelete(DeleteBehavior.Cascade);
        entity.HasIndex(charge => charge.TripReceiptId);
    }
}
