using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NVGInventory.Modules.Dispatching.Entities;
using NVGInventory.Modules.Dispatching.Enums;

namespace NVGInventory.Modules.Dispatching.Persistence.Configurations;

public sealed class DispatchOptimizationPlanConfiguration : IEntityTypeConfiguration<DispatchOptimizationPlan>
{
    public void Configure(EntityTypeBuilder<DispatchOptimizationPlan> entity)
    {
        var statusConverter = new ValueConverter<OptimizationPlanStatus, string>(
            value => value.ToString(),
            value => (OptimizationPlanStatus)Enum.Parse(typeof(OptimizationPlanStatus), value));

        entity.ToTable("dispatch_optimization_plans");
        entity.HasKey(plan => plan.Id);
        entity.Property(plan => plan.Id).HasColumnName("id");
        
        entity.Property(plan => plan.ScheduledDate)
            .HasColumnName("scheduled_date")
            .IsRequired();

        entity.Property(plan => plan.GeneratedByUserId)
            .HasColumnName("generated_by_user_id");

        entity.Property(plan => plan.Status)
            .HasColumnName("status")
            .HasConversion(statusConverter)
            .HasMaxLength(30)
            .IsRequired();

        entity.Property(plan => plan.AlgorithmName)
            .HasColumnName("algorithm_name")
            .HasMaxLength(100)
            .IsRequired();

        entity.Property(plan => plan.IsCompleteSolution)
            .HasColumnName("is_complete_solution")
            .IsRequired();

        entity.Property(plan => plan.SearchIterations)
            .HasColumnName("search_iterations")
            .IsRequired();

        entity.Property(plan => plan.MaxIterations)
            .HasColumnName("max_iterations")
            .IsRequired();

        entity.Property(plan => plan.MaxCandidatesPerState)
            .HasColumnName("max_candidates_per_state")
            .IsRequired();

        entity.Property(plan => plan.MaxExecutionSeconds)
            .HasColumnName("max_execution_seconds")
            .IsRequired();

        entity.Property(plan => plan.TotalEstimatedDistanceKm)
            .HasColumnName("total_estimated_distance_km")
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        entity.Property(plan => plan.TotalEstimatedEmptyMileageKm)
            .HasColumnName("total_estimated_empty_mileage_km")
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        entity.Property(plan => plan.TotalEstimatedTravelMinutes)
            .HasColumnName("total_estimated_travel_minutes")
            .IsRequired();

        entity.Property(plan => plan.TotalLatenessRisk)
            .HasColumnName("total_lateness_risk")
            .HasColumnType("decimal(5,4)")
            .IsRequired();

        entity.Property(plan => plan.FinalStateCost)
            .HasColumnName("final_state_cost")
            .HasColumnType("decimal(18,4)")
            .IsRequired();

        entity.Property(plan => plan.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        entity.Property(plan => plan.ApprovedAt)
            .HasColumnName("approved_at");

        entity.Property(plan => plan.ApprovedByUserId)
            .HasColumnName("approved_by_user_id");

        entity.HasOne(plan => plan.GeneratedByUser)
            .WithMany()
            .HasForeignKey(plan => plan.GeneratedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(plan => plan.ApprovedByUser)
            .WithMany()
            .HasForeignKey(plan => plan.ApprovedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
