using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NVGInventory.Modules.Dispatching.Entities;

namespace NVGInventory.Modules.Dispatching.Persistence.Configurations;

public sealed class DispatchRecommendationConfiguration : IEntityTypeConfiguration<DispatchRecommendation>
{
    public void Configure(EntityTypeBuilder<DispatchRecommendation> entity)
    {
        entity.ToTable("dispatch_recommendations");
        entity.HasKey(recommendation => recommendation.Id);
        entity.Property(recommendation => recommendation.Id).HasColumnName("id");
        entity.Property(recommendation => recommendation.CompletedTripId).HasColumnName("completed_trip_id");
        entity.Property(recommendation => recommendation.DriverId).HasColumnName("driver_id");
        entity.Property(recommendation => recommendation.TruckId).HasColumnName("truck_id");
        entity.Property(recommendation => recommendation.RecommendedTripId).HasColumnName("recommended_trip_id");
        entity.Property(recommendation => recommendation.ProximityScore)
            .HasColumnName("proximity_score")
            .HasColumnType("decimal(5,4)")
            .IsRequired();
        entity.Property(recommendation => recommendation.AvailabilityScore)
            .HasColumnName("availability_score")
            .HasColumnType("decimal(5,4)")
            .IsRequired();
        entity.Property(recommendation => recommendation.TruckMatchScore)
            .HasColumnName("truck_match_score")
            .HasColumnType("decimal(5,4)")
            .IsRequired();
        entity.Property(recommendation => recommendation.AgingScore)
            .HasColumnName("aging_score")
            .HasColumnType("decimal(5,4)")
            .IsRequired();
        entity.Property(recommendation => recommendation.TotalScore)
            .HasColumnName("total_score")
            .HasColumnType("decimal(5,4)")
            .IsRequired();
        entity.Property(recommendation => recommendation.Rank).HasColumnName("rank").IsRequired();
        entity.Property(recommendation => recommendation.GeneratedAt)
            .HasColumnName("generated_at")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();
        entity.Property(recommendation => recommendation.ExpiresAt).HasColumnName("expires_at").IsRequired();
        entity.Property(recommendation => recommendation.WasAccepted)
            .HasColumnName("was_accepted")
            .HasDefaultValue(false)
            .IsRequired();
        entity.Property(recommendation => recommendation.WasIgnored)
            .HasColumnName("was_ignored")
            .HasDefaultValue(false)
            .IsRequired();
        entity.Property(recommendation => recommendation.ReviewedByUserId).HasColumnName("reviewed_by_user_id");
        entity.Property(recommendation => recommendation.ReviewedAt).HasColumnName("reviewed_at");

        entity.HasOne(recommendation => recommendation.CompletedTrip)
            .WithMany()
            .HasForeignKey(recommendation => recommendation.CompletedTripId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(recommendation => recommendation.RecommendedTrip)
            .WithMany()
            .HasForeignKey(recommendation => recommendation.RecommendedTripId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(recommendation => recommendation.ReviewedByUser)
            .WithMany()
            .HasForeignKey(recommendation => recommendation.ReviewedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(recommendation => new
        {
            recommendation.ExpiresAt,
            recommendation.WasAccepted,
            recommendation.WasIgnored
        });
        entity.HasIndex(recommendation => recommendation.GeneratedAt);
        entity.HasIndex(recommendation => recommendation.CompletedTripId);
        entity.HasIndex(recommendation => recommendation.RecommendedTripId);
        entity.HasIndex(recommendation => recommendation.DriverId);
    }
}
