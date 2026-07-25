using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NVGInventory.Modules.Dispatching.Entities;

namespace NVGInventory.Modules.Dispatching.Persistence.Configurations;

public sealed class OptimizationWeightSettingsConfiguration : IEntityTypeConfiguration<OptimizationWeightSettings>
{
    public void Configure(EntityTypeBuilder<OptimizationWeightSettings> builder)
    {
        builder.ToTable("optimization_weight_settings");

        builder.HasKey(e => e.Id);
        
        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.DeadheadDistanceWeight).HasColumnName("deadhead_distance_weight").HasPrecision(5, 2).HasDefaultValue(0.25m).IsRequired();
        builder.Property(e => e.CleaningTimeWeight).HasColumnName("cleaning_time_weight").HasPrecision(5, 2).HasDefaultValue(0.15m).IsRequired();
        builder.Property(e => e.WaitingTimeWeight).HasColumnName("waiting_time_weight").HasPrecision(5, 2).HasDefaultValue(0.15m).IsRequired();
        builder.Property(e => e.JobUrgencyWeight).HasColumnName("job_urgency_weight").HasPrecision(5, 2).HasDefaultValue(0.20m).IsRequired();
        builder.Property(e => e.CargoCompatibilityWeight).HasColumnName("cargo_compatibility_weight").HasPrecision(5, 2).HasDefaultValue(0.15m).IsRequired();
        builder.Property(e => e.AssetUtilizationWeight).HasColumnName("asset_utilization_weight").HasPrecision(5, 2).HasDefaultValue(0.10m).IsRequired();

        builder.Property(e => e.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(e => e.CreatedByUserId)
            .HasColumnName("created_by_user_id");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("GETUTCDATE()")
            .IsRequired();

        builder.HasOne(e => e.CreatedByUser)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
