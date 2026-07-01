using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Exceptions;
using NVGInventory.Domain.Services;
using NVGInventory.Modules.Dispatching.Entities;
using NVGInventory.Modules.Dispatching.Enums;

namespace NVGInventory.Modules.Dispatching.Services;

public sealed class GeneratedWaybillService : IGeneratedWaybillService
{
    private readonly InventoryDbContext _dbContext;
    private readonly IAuditService _auditService;

    public GeneratedWaybillService(InventoryDbContext dbContext, IAuditService auditService)
    {
        _dbContext = dbContext;
        _auditService = auditService;
    }

    public async Task<GeneratedWaybill> GenerateAsync(
        Guid tripId,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default)
    {
        EnsureCanGenerate(actor);

        var trip = await _dbContext.DispatchTrips
            .Include(t => t.Customer)
            .Include(t => t.Driver)
            .Include(t => t.TruckAsset)
            .Include(t => t.Stops)
            .Include(t => t.Documents)
            .FirstOrDefaultAsync(t => t.Id == tripId, cancellationToken);

        if (trip is null)
        {
            throw new NotFoundException("Trip not found.");
        }

        if (trip.Status is not (TripStatus.Delivered or TripStatus.Closed))
        {
            throw new ConflictDomainException("Waybill can only be generated after delivery.");
        }

        if (string.IsNullOrWhiteSpace(trip.ContainerNumber))
        {
            throw new BusinessRuleViolationException("Container number is required before generating a waybill.");
        }

        var hasVerifiedEir = trip.Documents.Any(doc =>
            doc.IsActive &&
            doc.Type == TripDocumentType.Eir &&
            doc.State == TripDocumentState.Verified);
        if (!hasVerifiedEir)
        {
            throw new ConflictDomainException("Verified EIR is required before generating a waybill.");
        }

        var now = DateTime.UtcNow;
        var active = await _dbContext.GeneratedWaybills
            .Where(waybill => waybill.TripId == tripId && waybill.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var waybill in active)
        {
            waybill.IsActive = false;
        }

        var version = active.Count == 0
            ? await _dbContext.GeneratedWaybills.CountAsync(w => w.TripId == tripId, cancellationToken) + 1
            : active.Max(w => w.Version) + 1;

        var waybillNumber = await GenerateWaybillNumberAsync(now.Year, cancellationToken);
        var pickup = trip.Stops.FirstOrDefault(stop => stop.StopType == TripStopType.Pickup);
        var dropoff = trip.Stops.FirstOrDefault(stop => stop.StopType == TripStopType.Dropoff);
        var snapshot = new
        {
            WaybillNumber = waybillNumber,
            ContainerNumber = trip.ContainerNumber,
            EirNumber = trip.EirNumber,
            trip.BookingNumber,
            trip.ShippingLine,
            PickupLocation = pickup?.LocationText,
            DropoffLocation = dropoff?.LocationText,
            ScheduledPickupTime = pickup?.ScheduledAt,
            ActualPickupTime = pickup?.ActualAt,
            ScheduledDropoffTime = dropoff?.ScheduledAt,
            ActualDropoffTime = dropoff?.ActualAt,
            DriverName = trip.Driver?.Username,
            TruckPlate = trip.TruckAsset?.PlateNo ?? trip.TruckAsset?.AssetCode,
            CustomerName = trip.Customer?.Name,
            trip.Rate,
            GeneratedAt = now,
            GeneratedByUserId = actor.UserId,
            Version = version
        };

        var generated = new GeneratedWaybill
        {
            Id = Guid.NewGuid(),
            TripId = trip.Id,
            WaybillNumber = waybillNumber,
            Version = version,
            GeneratedAt = now,
            GeneratedByUserId = actor.UserId,
            WaybillDataJson = JsonSerializer.Serialize(snapshot),
            IsActive = true
        };

        trip.WaybillNumber = waybillNumber;
        trip.UpdatedAt = now;
        _dbContext.GeneratedWaybills.Add(generated);

        _auditService.AddEntry(
            actor.UserId,
            AuditActions.WaybillGenerated,
            EntityTypes.GeneratedWaybill,
            generated.Id,
            null,
            new { TripId = trip.Id, generated.WaybillNumber, generated.Version },
            tripId: trip.Id);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return generated;
    }

    public async Task<GeneratedWaybill?> GetActiveAsync(
        Guid tripId,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var trip = await _dbContext.DispatchTrips
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tripId, cancellationToken);

        if (trip is null)
        {
            throw new NotFoundException("Trip not found.");
        }

        EnsureTripAccess(trip, actor);

        return await _dbContext.GeneratedWaybills
            .AsNoTracking()
            .Where(waybill => waybill.TripId == tripId && waybill.IsActive)
            .OrderByDescending(waybill => waybill.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<string> GenerateWaybillNumberAsync(int year, CancellationToken cancellationToken)
    {
        var prefix = $"NVG-{year}-";
        var count = await _dbContext.GeneratedWaybills
            .AsNoTracking()
            .CountAsync(waybill => waybill.WaybillNumber.StartsWith(prefix), cancellationToken);

        return $"{prefix}{count + 1:00000}";
    }

    private static void EnsureCanGenerate(DispatchActorContext actor)
    {
        if (actor.IsDispatcher || actor.IsManager || actor.IsAdmin)
        {
            return;
        }

        throw new ForbiddenDomainException("Only dispatchers or managers can generate waybills.");
    }

    private static void EnsureTripAccess(Trip trip, DispatchActorContext actor)
    {
        if (actor.IsPrivileged)
        {
            return;
        }

        if (actor.IsDriver && trip.DriverUserId == actor.UserId)
        {
            return;
        }

        throw new ForbiddenDomainException("Trip access denied.");
    }
}
