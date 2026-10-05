using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NVGInventory.Modules.ShipmentRequests.Entities;

namespace NVGInventory.Modules.ShipmentRequests.Persistence.Configurations;

public sealed class BookingFinanceHistoryConfiguration : IEntityTypeConfiguration<BookingFinanceHistory>
{
    public void Configure(EntityTypeBuilder<BookingFinanceHistory> entity)
    {
        entity.ToTable("booking_finance_history");
        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).HasColumnName("id");
        entity.Property(item => item.ShipmentRequestId).HasColumnName("shipment_request_id");
        entity.Property(item => item.FromStatus).HasColumnName("from_status").HasConversion<string>().HasMaxLength(40);
        entity.Property(item => item.ToStatus).HasColumnName("to_status").HasConversion<string>().HasMaxLength(40);
        entity.Property(item => item.ActorUserId).HasColumnName("actor_user_id");
        entity.Property(item => item.ActorRole).HasColumnName("actor_role").HasMaxLength(200).IsRequired();
        entity.Property(item => item.Amount).HasColumnName("amount").HasColumnType("decimal(18,2)");
        entity.Property(item => item.PaymentMethod).HasColumnName("payment_method").HasMaxLength(40);
        entity.Property(item => item.ReferenceNumber).HasColumnName("reference_number").HasMaxLength(160);
        entity.Property(item => item.Reason).HasColumnName("reason").HasMaxLength(1000).IsRequired();
        entity.Property(item => item.ChangedAt).HasColumnName("changed_at").HasDefaultValueSql("SYSUTCDATETIME()");
        entity.HasOne(item => item.ShipmentRequest).WithMany(request => request.FinanceHistory).HasForeignKey(item => item.ShipmentRequestId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(item => item.Actor).WithMany().HasForeignKey(item => item.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(item => new { item.ShipmentRequestId, item.ChangedAt });
    }
}
