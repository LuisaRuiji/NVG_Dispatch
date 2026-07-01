using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NVGInventory.Modules.Dispatching.Entities;

namespace NVGInventory.Modules.Dispatching.Persistence.Configurations;

public sealed class GeneratedWaybillConfiguration : IEntityTypeConfiguration<GeneratedWaybill>
{
    public void Configure(EntityTypeBuilder<GeneratedWaybill> entity)
    {
        entity.ToTable("generated_waybills");
        entity.HasKey(waybill => waybill.Id);
        entity.Property(waybill => waybill.Id).HasColumnName("id");
        entity.Property(waybill => waybill.TripId).HasColumnName("trip_id");
        entity.Property(waybill => waybill.WaybillNumber).HasColumnName("waybill_number").HasMaxLength(30).IsRequired();
        entity.Property(waybill => waybill.Version).HasColumnName("version").IsRequired();
        entity.Property(waybill => waybill.GeneratedAt).HasColumnName("generated_at").HasDefaultValueSql("SYSUTCDATETIME()");
        entity.Property(waybill => waybill.GeneratedByUserId).HasColumnName("generated_by_user_id");
        entity.Property(waybill => waybill.WaybillDataJson).HasColumnName("waybill_data_json").IsRequired();
        entity.Property(waybill => waybill.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();

        entity.HasOne(waybill => waybill.Trip)
            .WithMany(trip => trip.GeneratedWaybills)
            .HasForeignKey(waybill => waybill.TripId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(waybill => waybill.GeneratedByUser)
            .WithMany()
            .HasForeignKey(waybill => waybill.GeneratedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(waybill => waybill.TripId);
        entity.HasIndex(waybill => new { waybill.TripId, waybill.IsActive });
        entity.HasIndex(waybill => waybill.WaybillNumber).IsUnique();
    }
}
