using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NVGInventory.Modules.Dispatching.Entities;

namespace NVGInventory.Modules.Dispatching.Persistence.Configurations;

/// <summary>Maps the one-per-trip record that prevents duplicate overdue postings.</summary>
public sealed class TripOverdueBalanceRecordConfiguration : IEntityTypeConfiguration<TripOverdueBalanceRecord>
{
    public void Configure(EntityTypeBuilder<TripOverdueBalanceRecord> entity)
    {
        entity.ToTable("dispatch_trip_overdue_balance_records");
        entity.HasKey(record => record.Id);
        entity.Property(record => record.Id).HasColumnName("id");
        entity.Property(record => record.TripId).HasColumnName("trip_id");
        entity.Property(record => record.CustomerId).HasColumnName("customer_id");
        entity.Property(record => record.Amount).HasColumnName("amount").HasColumnType("decimal(18,2)");
        entity.Property(record => record.Reason).HasColumnName("reason").HasMaxLength(600).IsRequired();
        entity.Property(record => record.RecordedByUserId).HasColumnName("recorded_by_user_id");
        entity.Property(record => record.RecordedAt).HasColumnName("recorded_at").HasDefaultValueSql("SYSUTCDATETIME()");
        entity.HasOne(record => record.Trip).WithMany(trip => trip.OverdueBalanceRecords).HasForeignKey(record => record.TripId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(record => record.RecordedByUser).WithMany().HasForeignKey(record => record.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(record => record.TripId).IsUnique();
        entity.HasIndex(record => new { record.CustomerId, record.RecordedAt });
    }
}
