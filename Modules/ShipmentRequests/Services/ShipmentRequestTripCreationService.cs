using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Exceptions;
using NVGInventory.Modules.Dispatching.Services;
using NVGInventory.Modules.ShipmentRequests.Enums;

namespace NVGInventory.Modules.ShipmentRequests.Services;

public sealed class ShipmentRequestTripCreationService : IShipmentRequestTripCreationService
{
    private readonly InventoryDbContext _dbContext;
    private readonly IShipmentRequestTripDispatchGateway _tripDispatchGateway;

    public ShipmentRequestTripCreationService(
        InventoryDbContext dbContext,
        IShipmentRequestTripDispatchGateway tripDispatchGateway)
    {
        _dbContext = dbContext;
        _tripDispatchGateway = tripDispatchGateway;
    }

    public async Task<Guid> CreateDraftTripFromApprovedRequestAsync(
        CreateTripFromShipmentRequestCommand command,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var request = await _dbContext.ShipmentRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == command.ShipmentRequestId, cancellationToken);

        if (request is null)
        {
            throw new NotFoundException("Shipment request not found.");
        }

        if (request.Status != ShipmentRequestStatus.Approved)
        {
            throw new ConflictDomainException("Only approved requests can be converted.");
        }

        if (request.ConvertedTripId.HasValue)
        {
            throw new ConflictDomainException("This request has already been converted.");
        }

        var pickupTime = command.ScheduledPickupTimeOverride ?? request.RequestedPickupTime;
        if (!pickupTime.HasValue)
        {
            throw new BusinessRuleViolationException("Requested pickup time is required to convert.");
        }

        var draft = new ShipmentRequestTripDraftData(
            request.CustomerId,
            request.PickupLocation,
            request.DropoffLocation,
            pickupTime.Value,
            request.ContainerNumber,
            request.BookingNumber,
            request.ShippingLine,
            request.ContainerSize,
            request.TripType,
            request.PickupLatitude,
            request.PickupLongitude,
            request.DropoffLatitude,
            request.DropoffLongitude);

        return await _tripDispatchGateway.CreateDraftTripAsync(draft, actor, cancellationToken);
    }
}
