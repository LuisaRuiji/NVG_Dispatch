using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Exceptions;
using NVGInventory.Domain.Services;
using NVGInventory.Hubs;
using NVGInventory.Hubs.Events;
using NVGInventory.Modules.Dispatching.Entities;
using NVGInventory.Modules.Dispatching.Enums;
using NVGInventory.Modules.Dispatching.Services;
using NVGInventory.Modules.ShipmentRequests.Entities;
using NVGInventory.Modules.ShipmentRequests.Enums;
using NVGInventory.Services;

namespace NVGInventory.Modules.ShipmentRequests.Services;

public sealed record CreateShipmentRequestCommand(
    Guid CustomerId,
    string PickupLocation,
    decimal? PickupLatitude,
    decimal? PickupLongitude,
    string DropoffLocation,
    decimal? DropoffLatitude,
    decimal? DropoffLongitude,
    DateTime? RequestedPickupTime,
    ContainerSize ContainerSize,
    TripType TripType,
    string? ContainerNumber,
    string? ShippingLine,
    string? BookingNumber,
    string? CargoDescription,
    decimal? CargoWeight,
    string? SpecialInstructions,
    Guid CreatedByUserId);

public sealed record UpdateShipmentRequestCommand(
    Guid CustomerId,
    string PickupLocation,
    decimal? PickupLatitude,
    decimal? PickupLongitude,
    string DropoffLocation,
    decimal? DropoffLatitude,
    decimal? DropoffLongitude,
    DateTime? RequestedPickupTime,
    ContainerSize ContainerSize,
    TripType TripType,
    string? ContainerNumber,
    string? ShippingLine,
    string? BookingNumber,
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
    private readonly IShipmentRequestTripCreationService _tripCreationService;
    private readonly IAuditService? _auditService;
    private readonly IServiceScopeFactory? _serviceScopeFactory;
    private readonly IVaiaCacheService? _cacheService;

    public ShipmentRequestService(
        InventoryDbContext dbContext,
        UserService userService,
        IShipmentRequestTripCreationService tripCreationService,
        IAuditService? auditService = null,
        IServiceScopeFactory? serviceScopeFactory = null,
        IVaiaCacheService? cacheService = null)
    {
        _dbContext = dbContext;
        _userService = userService;
        _tripCreationService = tripCreationService;
        _auditService = auditService;
        _serviceScopeFactory = serviceScopeFactory;
        _cacheService = cacheService;
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
            PickupLatitude = command.PickupLatitude,
            PickupLongitude = command.PickupLongitude,
            DropoffLocation = command.DropoffLocation.Trim(),
            DropoffLatitude = command.DropoffLatitude,
            DropoffLongitude = command.DropoffLongitude,
            RequestedPickupTime = command.RequestedPickupTime,
            ContainerSize = ToStorageValue(command.ContainerSize),
            TripType = ToStorageValue(command.TripType),
            ContainerNumber = Normalize(command.ContainerNumber),
            ShippingLine = Normalize(command.ShippingLine),
            BookingNumber = Normalize(command.BookingNumber),
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
        await InvalidateShipmentRequestCachesAsync(request.CustomerId, cancellationToken);
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
        request.PickupLatitude = command.PickupLatitude;
        request.PickupLongitude = command.PickupLongitude;
        request.DropoffLocation = command.DropoffLocation.Trim();
        request.DropoffLatitude = command.DropoffLatitude;
        request.DropoffLongitude = command.DropoffLongitude;
        request.RequestedPickupTime = command.RequestedPickupTime;
        request.ContainerSize = ToStorageValue(command.ContainerSize);
        request.TripType = ToStorageValue(command.TripType);
        request.ContainerNumber = Normalize(command.ContainerNumber);
        request.ShippingLine = Normalize(command.ShippingLine);
        request.BookingNumber = Normalize(command.BookingNumber);
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
        await InvalidateShipmentRequestCachesAsync(request.CustomerId, cancellationToken);
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

        var submittedAt = DateTime.UtcNow;
        request.Status = ShipmentRequestStatus.Submitted;

        _auditService?.AddEntry(
            actorUserId,
            "SHIPMENT_REQUEST_SUBMITTED",
            "shipment_request",
            request.Id,
            null,
            new { request.Status });

        await _dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateShipmentRequestCachesAsync(request.CustomerId, cancellationToken);
        QueueShipmentRequestSubmittedBroadcast(request.Id, submittedAt);
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
        await InvalidateShipmentRequestCachesAsync(request.CustomerId, cancellationToken);
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
        await InvalidateShipmentRequestCachesAsync(request.CustomerId, cancellationToken);
        await QueuePushToCustomerUsersAsync(
            request.CustomerId,
            "Shipment Request Approved",
            "Your request has been approved and is being processed",
            new Dictionary<string, string> { ["requestId"] = request.Id.ToString() },
            cancellationToken);
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
        await InvalidateShipmentRequestCachesAsync(request.CustomerId, cancellationToken);
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

        var tripId = await _tripCreationService.CreateDraftTripFromApprovedRequestAsync(
            new CreateTripFromShipmentRequestCommand(
                request.Id,
                request.RequestedPickupTime),
            actor,
            cancellationToken);

        request.Status = ShipmentRequestStatus.ConvertedToTrip;
        request.ConvertedTripId = tripId;
        await CarryOverAtwToTripAsync(request.Id, tripId, actor.UserId, cancellationToken);

        _auditService?.AddEntry(
            actor.UserId,
            "SHIPMENT_REQUEST_CONVERTED",
            "shipment_request",
            request.Id,
            null,
            new { request.Status, request.ConvertedTripId });

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await InvalidateShipmentRequestCachesAsync(request.CustomerId, cancellationToken);

        return (request, tripId);
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

    private static string ToStorageValue(ContainerSize value)
    {
        return value switch
        {
            ContainerSize.TwentyFt => "TWENTY_FT",
            ContainerSize.FortyFt => "FORTY_FT",
            ContainerSize.FortyHC => "FORTY_HC",
            _ => "TWENTY_FT"
        };
    }

    private static string ToStorageValue(TripType value)
    {
        return value switch
        {
            TripType.PortPickup => "PORT_PICKUP",
            TripType.PortDropoff => "PORT_DROPOFF",
            TripType.YardTransfer => "YARD_TRANSFER",
            TripType.LongHaul => "LONG_HAUL",
            _ => "PORT_PICKUP"
        };
    }

    private async Task CarryOverAtwToTripAsync(
        Guid requestId,
        Guid tripId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var atw = await _dbContext.ShipmentRequestDocuments
            .AsNoTracking()
            .Where(doc => doc.RequestId == requestId && doc.DocumentType == ShipmentRequestDocumentType.Atw)
            .OrderByDescending(doc => doc.UploadedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (atw is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        _dbContext.DispatchTripDocuments.Add(new TripDocument
        {
            Id = Guid.NewGuid(),
            TripId = tripId,
            Type = TripDocumentType.Atw,
            State = TripDocumentState.Uploaded,
            StorageKey = atw.StorageKey,
            UploadedByUserId = actorUserId,
            UploadedAt = now,
            IsActive = true,
            Remarks = "ATW carried over from shipment request."
        });
    }

    private async Task InvalidateShipmentRequestCachesAsync(Guid customerId, CancellationToken cancellationToken)
    {
        if (_cacheService is null)
        {
            return;
        }

        _cacheService.Invalidate(VaiaCacheKeys.DispatchKpis);

        var customerUserIds = await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.CustomerId == customerId)
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);
        foreach (var customerUserId in customerUserIds)
        {
            _cacheService.Invalidate(VaiaCacheKeys.CustomerKpis(customerUserId));
        }
    }

    private async Task QueuePushToCustomerUsersAsync(
        Guid customerId,
        string title,
        string body,
        Dictionary<string, string>? data,
        CancellationToken cancellationToken)
    {
        if (_serviceScopeFactory is null)
        {
            return;
        }

        var customerUserIds = await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.CustomerId == customerId)
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);
        foreach (var userId in customerUserIds)
        {
            QueuePushToUser(userId, title, body, data);
        }
    }

    private void QueuePushToUser(
        Guid userId,
        string title,
        string body,
        Dictionary<string, string>? data = null)
    {
        if (_serviceScopeFactory is null)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<ShipmentRequestService>>();
            try
            {
                var pushService = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();
                await pushService.SendToUserAsync(userId, title, body, data);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to queue shipment request push notification for user {UserId}.", userId);
            }
        });
    }

    private void QueueShipmentRequestSubmittedBroadcast(Guid requestId, DateTime submittedAt)
    {
        if (_serviceScopeFactory is null)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<ShipmentRequestService>>();
            try
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
                var hubContext = scope.ServiceProvider
                    .GetRequiredService<IHubContext<VaiaDispatchHub, IVaiaDispatchClient>>();
                var request = await dbContext.ShipmentRequests
                    .Include(item => item.Customer)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == requestId);
                if (request is null)
                {
                    return;
                }

                var e = new ShipmentRequestSubmittedEvent(
                    request.Id,
                    request.Customer?.Name ?? "Unknown customer",
                    request.PickupLocation,
                    request.DropoffLocation,
                    submittedAt);

                await hubContext.Clients
                    .Group(VaiaDispatchHub.DispatchOpsGroup)
                    .ShipmentRequestSubmitted(e);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Failed to broadcast shipment request submission for request {RequestId}.",
                    requestId);
            }
        });
    }
}
