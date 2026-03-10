using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NVGInventory.Modules.Dispatching.Entities;
using NVGInventory.Modules.Dispatching.Enums;

namespace NVGInventory.Modules.Dispatching.Persistence.Configurations;

public sealed class TripStatusHistoryConfiguration : IEntityTypeConfiguration<TripStatusHistory>
{
    public void Configure(EntityTypeBuilder<TripStatusHistory> entity)
    {
        var statusConverter = new ValueConverter<TripStatus, string>(
            value =>
                value == TripStatus.Draft ? "DRAFT" :
                value == TripStatus.Dispatched ? "DISPATCHED" :
                value == TripStatus.EnroutePickup ? "ENROUTE_PICKUP" :
                value == TripStatus.AtPickup ? "AT_PICKUP" :
                value == TripStatus.Loaded ? "LOADED" :
                value == TripStatus.EnrouteDropoff ? "ENROUTE_DROPOFF" :
                value == TripStatus.AtDropoff ? "AT_DROPOFF" :
                value == TripStatus.Delivered ? "DELIVERED" :
                value == TripStatus.Closed ? "CLOSED" :
                value == TripStatus.Cancelled ? "CANCELLED" :
                value == TripStatus.OnHold ? "ON_HOLD" :
                value == TripStatus.FailedAttempt ? "FAILED_ATTEMPT" :
                "DRAFT",
            value =>
                value == "DRAFT" ? TripStatus.Draft :
                value == "DISPATCHED" ? TripStatus.Dispatched :
                value == "ENROUTE_PICKUP" ? TripStatus.EnroutePickup :
                value == "AT_PICKUP" ? TripStatus.AtPickup :
                value == "LOADED" ? TripStatus.Loaded :
                value == "ENROUTE_DROPOFF" ? TripStatus.EnrouteDropoff :
                value == "AT_DROPOFF" ? TripStatus.AtDropoff :
                value == "DELIVERED" ? TripStatus.Delivered :
                value == "CLOSED" ? TripStatus.Closed :
                value == "CANCELLED" ? TripStatus.Cancelled :
                value == "ON_HOLD" ? TripStatus.OnHold :
                value == "FAILED_ATTEMPT" ? TripStatus.FailedAttempt :
                TripStatus.Draft);

        var historyEventConverter = new ValueConverter<TripHistoryEventType, string>(
            value =>
                value == TripHistoryEventType.ScheduleUpdated ? "SCHEDULE_UPDATED" :
                value == TripHistoryEventType.StatusCorrected ? "STATUS_CORRECTED" :
                "STATUS_CHANGE",
            value =>
                value == "SCHEDULE_UPDATED" ? TripHistoryEventType.ScheduleUpdated :
                value == "STATUS_CORRECTED" ? TripHistoryEventType.StatusCorrected :
                TripHistoryEventType.StatusChange);

        entity.ToTable("dispatch_trip_status_history");
        entity.HasKey(history => history.Id);
        entity.Property(history => history.Id).HasColumnName("id");
        entity.Property(history => history.TripId).HasColumnName("trip_id");
        entity.Property(history => history.EventType)
            .HasColumnName("event_type")
            .HasConversion(historyEventConverter)
            .HasMaxLength(30)
            .HasDefaultValue(TripHistoryEventType.StatusChange)
            .IsRequired();
        entity.Property(history => history.FromStatus)
            .HasColumnName("from_status")
            .HasConversion(statusConverter)
            .HasMaxLength(30)
            .IsRequired();
        entity.Property(history => history.ToStatus)
            .HasColumnName("to_status")
            .HasConversion(statusConverter)
            .HasMaxLength(30)
            .IsRequired();
        entity.Property(history => history.ActorUserId).HasColumnName("actor_user_id");
        entity.Property(history => history.Remarks).HasColumnName("remarks").HasMaxLength(400);
        entity.Property(history => history.EventAt).HasColumnName("event_at");
        entity.Property(history => history.RecordedAt)
            .HasColumnName("recorded_at")
            .HasDefaultValueSql("SYSUTCDATETIME()");

        entity.HasOne(history => history.Trip)
            .WithMany(trip => trip.StatusHistory)
            .HasForeignKey(history => history.TripId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(history => history.Actor)
            .WithMany()
            .HasForeignKey(history => history.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(history => history.TripId);
        entity.HasIndex(history => new { history.TripId, history.EventAt });
        entity.HasIndex(history => new { history.TripId, history.RecordedAt });
    }
}
