using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Exceptions;
using NVGInventory.Domain.Services;
using NVGInventory.Modules.Dispatching.Enums;
using NVGInventory.Modules.Dispatching.Services;
using NVGInventory.Modules.ShipmentRequests.Entities;
using NVGInventory.Modules.ShipmentRequests.Enums;

namespace NVGInventory.Modules.ShipmentRequests.Services;

public sealed record CreateShipmentRequestCommand(
    Guid CustomerId,
    string PickupLocation,
    string DropoffLocation,
    DateTime? RequestedPickupTime,
    string? CargoDescription,
    decimal? CargoWeight,
    string? SpecialInstructions,
    Guid CreatedByUserId);

public sealed record UpdateShipmentRequestCommand(
    Guid CustomerId,
    string PickupLocation,
    string DropoffLocation,
    DateTime? RequestedPickupTime,
    string? CargoDescription,
    decimal? CargoWeight,
    string? SpecialInstructions,
    Guid UpdatedByUserId);

public sealed record UploadShipmentRequestDocumentCommand(
    Guid RequestId,
    ShipmentRequestDocumentType DocumentType,
    string StorageKey,
    Guid UploadedByUserId);

public sealed class ShipmentRequestService
{
    private readonly InventoryDbContext _dbContext;
    private readonly UserService _userService;
    private readonly DispatchTripService _dispatchTripService;
    private readonly IAuditService? _auditService;

    public ShipmentRequestService(
        InventoryDbContext dbContext,
        UserService userService,
        DispatchTripService dispatchTripService,
        IAuditService? auditService = null)
    {
        _dbContext = dbContext;
        _userService = userService;
        _dispatchTripService = dispatchTripService;
        _auditService = auditService;
    }

    public async Task<ShipmentRequest> CreateDraftAsync(
        CreateShipmentRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        await _userService.EnsureActiveUserAsync(command.CreatedByUserId, cancellationToken);
        await EnsureCustomerExistsAsync(command.CustomerId, cancellationToken);
        EnsureLocations(command.PickupLocation, command.DropoffLocation);

        var now = DateTime.UtcNow;
        var request = new ShipmentRequest
        {
            Id = Guid.NewGuid(),
            CustomerId = command.CustomerId,
            Status = ShipmentRequestStatus.Draft,
            PickupLocation = command.PickupLocation.Trim(),
            DropoffLocation = command.DropoffLocation.Trim(),
            RequestedPickupTime = command.RequestedPickupTime,
            CargoDescription = Normalize(command.CargoDescription),
            CargoWeight = command.CargoWeight,
            SpecialInstructions = Normalize(command.SpecialInstructions),
            CreatedAt = now,
            CreatedByUserId = command.CreatedByUserId
        };

        _dbContext.ShipmentRequests.Add(request);

        _auditService?.AddEntry(
            command.CreatedByUserId,
            "SHIPMENT_REQUEST_CREATED",
            "shipment_request",
            request.Id,
            null,
            new { request.Status, request.CustomerId });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return request;
    }

    public async Task<ShipmentRequest> UpdateDraftAsync(
        Guid requestId,
        UpdateShipmentRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        await _userService.EnsureActiveUserAsync(command.UpdatedByUserId, cancellationToken);

        var request = await _dbContext.ShipmentRequests
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (request is null || request.CustomerId != command.CustomerId)
        {
            throw new NotFoundException("Shipment request not found.");
        }

        if (request.Status != ShipmentRequestStatus.Draft)
        {
            throw new ConflictDomainException("Submitted requests cannot be modified.");
        }

        EnsureLocations(command.PickupLocation, command.DropoffLocation);

        request.PickupLocation = command.PickupLocation.Trim();
        request.DropoffLocation = command.DropoffLocation.Trim();
        request.RequestedPickupTime = command.RequestedPickupTime;
        request.CargoDescription = Normalize(command.CargoDescription);
        request.CargoWeight = command.CargoWeight;
        request.SpecialInstructions = Normalize(command.SpecialInstructions);

        _auditService?.AddEntry(
            command.UpdatedByUserId,
            "SHIPMENT_REQUEST_UPDATED",
            "shipment_request",
            request.Id,
            null,
            new { request.Status });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return request;
    }

    public async Task<ShipmentRequest> SubmitAsync(
        Guid requestId,
        Guid customerId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await _userService.EnsureActiveUserAsync(actorUserId, cancellationToken);

        var request = await _dbContext.ShipmentRequests
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (request is null || request.CustomerId != customerId)
        {
            throw new NotFoundException("Shipment request not found.");
        }

        if (request.Status != ShipmentRequestStatus.Draft)
        {
            throw new ConflictDomainException("Only draft requests can be submitted.");
        }

        request.Status = ShipmentRequestStatus.Submitted;

        _auditService?.AddEntry(
            actorUserId,
            "SHIPMENT_REQUEST_SUBMITTED",
            "shipment_request",
            request.Id,
            null,
            new { request.Status });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return request;
    }

    public async Task<ShipmentRequestDocument> UploadDocumentAsync(
        UploadShipmentRequestDocumentCommand command,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        await _userService.EnsureActiveUserAsync(command.UploadedByUserId, cancellationToken);

        if (string.IsNullOrWhiteSpace(command.StorageKey))
        {
            throw new BusinessRuleViolationException("StorageKey is required.");
        }

        var request = await _dbContext.ShipmentRequests
            .FirstOrDefaultAsync(r => r.Id == command.RequestId, cancellationToken);

        if (request is null || request.CustomerId != customerId)
        {
            throw new NotFoundException("Shipment request not found.");
        }

        if (request.Status is ShipmentRequestStatus.Approved
            or ShipmentRequestStatus.Rejected
            or ShipmentRequestStatus.ConvertedToTrip)
        {
            throw new ConflictDomainException("Documents cannot be uploaded for finalized requests.");
        }

        var now = DateTime.UtcNow;
        var doc = new ShipmentRequestDocument
        {
            Id = Guid.NewGuid(),
            RequestId = request.Id,
            DocumentType = command.DocumentType,
            StorageKey = command.StorageKey.Trim(),
            UploadedByUserId = command.UploadedByUserId,
            UploadedAt = now
        };

        _dbContext.ShipmentRequestDocuments.Add(doc);

        _auditService?.AddEntry(
            command.UploadedByUserId,
            "SHIPMENT_REQUEST_DOCUMENT_UPLOADED",
            "shipment_request_document",
            doc.Id,
            null,
            new { request.Id, doc.DocumentType });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return doc;
    }

    public async Task<ShipmentRequest> ApproveAsync(
        Guid requestId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await _userService.EnsureActiveUserAsync(actorUserId, cancellationToken);

        var request = await _dbContext.ShipmentRequests
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (request is null)
        {
            throw new NotFoundException("Shipment request not found.");
        }

        if (request.Status != ShipmentRequestStatus.Submitted)
        {
            throw new ConflictDomainException("Only submitted requests can be approved.");
        }

        request.Status = ShipmentRequestStatus.Approved;
        request.ApprovedAt = DateTime.UtcNow;
        request.ApprovedByUserId = actorUserId;

        _auditService?.AddEntry(
            actorUserId,
            "SHIPMENT_REQUEST_APPROVED",
            "shipment_request",
            request.Id,
            null,
            new { request.Status });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return request;
    }

    public async Task<ShipmentRequest> RejectAsync(
        Guid requestId,
        Guid actorUserId,
        string remarks,
        CancellationToken cancellationToken = default)
    {
        await _userService.EnsureActiveUserAsync(actorUserId, cancellationToken);

        if (string.IsNullOrWhiteSpace(remarks))
        {
            throw new BusinessRuleViolationException("Remarks are required.");
        }

        var request = await _dbContext.ShipmentRequests
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (request is null)
        {
            throw new NotFoundException("Shipment request not found.");
        }

        if (request.Status != ShipmentRequestStatus.Submitted)
        {
            throw new ConflictDomainException("Only submitted requests can be rejected.");
        }

        request.Status = ShipmentRequestStatus.Rejected;

        _auditService?.AddEntry(
            actorUserId,
            "SHIPMENT_REQUEST_REJECTED",
            "shipment_request",
            request.Id,
            null,
            new { request.Status, Remarks = remarks });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return request;
    }

    public async Task<(ShipmentRequest Request, Guid TripId)> ConvertToTripAsync(
        Guid requestId,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default)
    {
        await _userService.EnsureActiveUserAsync(actor.UserId, cancellationToken);

        var request = await _dbContext.ShipmentRequests
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

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

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        if (!request.RequestedPickupTime.HasValue)
        {
            throw new BusinessRuleViolationException("Requested pickup time is required to convert.");
        }

        var pickupTime = request.RequestedPickupTime.Value;

        var command = new CreateDispatchTripCommand(
            request.CustomerId,
            null,
            null,
            null,
            new[]
            {
                new DispatchTripStopInput(
                    TripStopType.Pickup,
                    request.PickupLocation,
                    pickupTime),
                new DispatchTripStopInput(
                    TripStopType.Dropoff,
                    request.DropoffLocation,
                    pickupTime)
            });

        var trip = await _dispatchTripService.CreateDraftAsync(command, actor, cancellationToken);

        request.Status = ShipmentRequestStatus.ConvertedToTrip;
        request.ConvertedTripId = trip.Id;

        _auditService?.AddEntry(
            actor.UserId,
            "SHIPMENT_REQUEST_CONVERTED",
            "shipment_request",
            request.Id,
            null,
            new { request.Status, request.ConvertedTripId });

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return (request, trip.Id);
    }

    private async Task EnsureCustomerExistsAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var exists = await _dbContext.DispatchCustomers
            .AsNoTracking()
            .AnyAsync(c => c.Id == customerId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException("Customer not found.");
        }
    }

    private static void EnsureLocations(string pickup, string dropoff)
    {
        if (string.IsNullOrWhiteSpace(pickup))
        {
            throw new BusinessRuleViolationException("PickupLocation is required.");
        }

        if (string.IsNullOrWhiteSpace(dropoff))
        {
            throw new BusinessRuleViolationException("DropoffLocation is required.");
        }
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
