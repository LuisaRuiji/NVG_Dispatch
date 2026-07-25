using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NVGInventory.Modules.Dispatching.Entities;

namespace NVGInventory.Modules.Dispatching.Persistence.Configurations;

public sealed class DispatchOptimizationRouteStopConfiguration : IEntityTypeConfiguration<DispatchOptimizationRouteStop>
{
    public void Configure(EntityTypeBuilder<DispatchOptimizationRouteStop> entity)
    {
        entity.ToTable("dispatch_optimization_route_stops");
        entity.HasKey(stop => stop.Id);
        entity.Property(stop => stop.Id).HasColumnName("id");
        
        entity.Property(stop => stop.RouteId).HasColumnName("route_id");
        entity.Property(stop => stop.TripId).HasColumnName("trip_id");
        
        entity.Property(stop => stop.StopOrder)
            .HasColumnName("stop_order")
            .IsRequired();

        entity.Property(stop => stop.EstimatedArrivalAtPickup)
            .HasColumnName("estimated_arrival_at_pickup");

        entity.Property(stop => stop.EstimatedArrivalAtDropoff)
            .HasColumnName("estimated_arrival_at_dropoff");

        entity.Property(stop => stop.EmptyTravelMinutesToPickup)
            .HasColumnName("empty_travel_minutes_to_pickup")
            .IsRequired();

        entity.Property(stop => stop.LoadedTravelMinutesToDropoff)
            .HasColumnName("loaded_travel_minutes_to_dropoff")
            .IsRequired();

        entity.Property(stop => stop.LatenessRiskScore)
            .HasColumnName("lateness_risk_score")
            .HasColumnType("decimal(5,4)")
            .IsRequired();

        entity.Property(stop => stop.PriorityUrgencyScore)
            .HasColumnName("priority_urgency_score")
            .HasColumnType("decimal(5,4)")
            .IsRequired();

        entity.Property(stop => stop.AssignmentScore)
            .HasColumnName("assignment_score")
            .HasColumnType("decimal(5,4)")
            .IsRequired();

        entity.HasOne(stop => stop.Route)
            .WithMany(route => route.RouteStops)
            .HasForeignKey(stop => stop.RouteId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(stop => stop.Trip)
            .WithMany()
            .HasForeignKey(stop => stop.TripId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
