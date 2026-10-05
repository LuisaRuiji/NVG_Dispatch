using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NVGInventory.Modules.Dispatching.Entities;

namespace NVGInventory.Modules.Dispatching.Persistence.Configurations;

public sealed class ContainerQualityInspectionConfiguration : IEntityTypeConfiguration<ContainerQualityInspection>
{
    public void Configure(EntityTypeBuilder<ContainerQualityInspection> entity)
    {
        entity.ToTable("dispatch_container_quality_inspections");
        entity.HasKey(item => item.TripId);
        entity.Property(item => item.TripId).HasColumnName("trip_id");
        entity.Property(item => item.HasDents).HasColumnName("has_dents");
        entity.Property(item => item.HasHoles).HasColumnName("has_holes");
        entity.Property(item => item.HasRust).HasColumnName("has_rust");
        entity.Property(item => item.HasOdor).HasColumnName("has_odor");
        entity.Property(item => item.HasResidue).HasColumnName("has_residue");
        entity.Property(item => item.HasStains).HasColumnName("has_stains");
        entity.Property(item => item.HasInsects).HasColumnName("has_insects");
        entity.Property(item => item.IsClean).HasColumnName("is_clean");
        entity.Property(item => item.FoodGradeRequired).HasColumnName("food_grade_required");
        entity.Property(item => item.FoodGradePassed).HasColumnName("food_grade_passed");
        entity.Property(item => item.Outcome).HasColumnName("outcome").HasConversion<string>().HasMaxLength(40);
        entity.Property(item => item.Reason).HasColumnName("reason").HasMaxLength(1000);
        entity.Property(item => item.InspectedByUserId).HasColumnName("inspected_by_user_id");
        entity.Property(item => item.InspectedAt).HasColumnName("inspected_at");
        entity.Property(item => item.ReviewedByUserId).HasColumnName("reviewed_by_user_id");
        entity.Property(item => item.ReviewedAt).HasColumnName("reviewed_at");
        entity.Property(item => item.RowVersion).HasColumnName("row_version").IsRowVersion();
        entity.HasOne(item => item.Trip).WithOne(trip => trip.ContainerInspection).HasForeignKey<ContainerQualityInspection>(item => item.TripId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(item => item.InspectedByUser).WithMany().HasForeignKey(item => item.InspectedByUserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.ReviewedByUser).WithMany().HasForeignKey(item => item.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
