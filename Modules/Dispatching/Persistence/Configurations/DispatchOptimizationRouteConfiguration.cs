using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NVGInventory.Modules.Dispatching.Entities;

namespace NVGInventory.Modules.Dispatching.Persistence.Configurations;

public sealed class DispatchOptimizationRouteConfiguration : IEntityTypeConfiguration<DispatchOptimizationRoute>
{
    public void Configure(EntityTypeBuilder<DispatchOptimizationRoute> entity)
    {
        entity.ToTable("dispatch_optimization_routes");
        entity.HasKey(route => route.Id);
        entity.Property(route => route.Id).HasColumnName("id");
        
        entity.Property(route => route.PlanId).HasColumnName("plan_id");
        entity.Property(route => route.DispatchTruckId).HasColumnName("dispatch_truck_id");
        entity.Property(route => route.DispatchDriverId).HasColumnName("dispatch_driver_id");
        
        entity.Property(route => route.SequenceNumber)
            .HasColumnName("sequence_number")
            .IsRequired();

        entity.Property(route => route.EstimatedDistanceKm)
            .HasColumnName("estimated_distance_km")
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        entity.Property(route => route.EstimatedEmptyMileageKm)
            .HasColumnName("estimated_empty_mileage_km")
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        entity.Property(route => route.EstimatedTravelMinutes)
            .HasColumnName("estimated_travel_minutes")
            .IsRequired();

        entity.Property(route => route.WorkloadScore)
            .HasColumnName("workload_score")
            .HasColumnType("decimal(18,4)")
            .IsRequired();

        entity.Property(route => route.RouteCost)
            .HasColumnName("route_cost")
            .HasColumnType("decimal(18,4)")
            .IsRequired();

        entity.HasOne(route => route.Plan)
            .WithMany(plan => plan.Routes)
            .HasForeignKey(route => route.PlanId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(route => route.DispatchTruck)
            .WithMany()
            .HasForeignKey(route => route.DispatchTruckId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(route => route.DispatchDriver)
            .WithMany()
            .HasForeignKey(route => route.DispatchDriverId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
