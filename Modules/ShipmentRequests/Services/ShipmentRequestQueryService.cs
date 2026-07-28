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

public sealed record ShipmentRequestListItem(
    Guid Id,
    ShipmentRequestStatus Status,
    string PickupLocation,
    string DropoffLocation,
    DateTime? RequestedPickupTime,
    ContainerSize ContainerSize,
    TripType TripType,
    string? ContainerNumber,
    string? ShippingLine,
    string? BookingNumber,
    int DocumentsCount,
    DateTime CreatedAt,
    DateTime? ApprovedAt,
    Guid? ConvertedTripId);

public sealed record ShipmentRequestDetail(
    Guid Id,
    ShipmentRequestStatus Status,
    string PickupLocation,
    string DropoffLocation,
    DateTime? RequestedPickupTime,
    ContainerSize ContainerSize,
    TripType TripType,
    string? ContainerNumber,
    string? ShippingLine,
    string? BookingNumber,
    string? CargoDescription,
    decimal? CargoWeight,
    string? SpecialInstructions,
    string? ReviewRemarks,
    DateTime CreatedAt,
    DateTime? ApprovedAt,
    Guid? ConvertedTripId,
    IReadOnlyCollection<ShipmentRequestDocument> Documents);

public sealed record DispatchShipmentRequestQueueItem(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
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
    int DocumentsCount,
    Guid? AtwDocumentId,
    string? AtwOriginalFileName,
    DocumentAnalysisStatus? AtwAnalysisStatus,
    DateTime? AtwUploadedAt,
    DateTime CreatedAt,
    ShipmentRequestStatus Status,
    ShipmentRequestQueuePriority Priority,
    string? ReviewRemarks);

public enum ShipmentRequestQueuePriority
{
    Critical,
    High,
    Normal
}

public enum ShipmentRequestQueueAtwStatus
{
    Missing,
    Uploaded
}

public enum ShipmentRequestQueueSort
{
    Priority,
    RequestedTime,
    RequestedTimeDescending,
    Newest,
    Oldest,
    Customer
}

public sealed record DispatchShipmentRequestActivity(
    string Action,
    string? ActorUsername,
    DateTime CreatedAt);

public sealed record DispatchShipmentRequestAssignment(
    Guid TripId,
    Guid? DriverUserId,
    string? DriverUsername,
    Guid? TruckAssetId,
    string? TruckAssetCode,
    Guid? TrailerAssetId,
    string? TrailerAssetCode);

public sealed record DispatchShipmentRequestDetail(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    ShipmentRequestStatus Status,
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
    string? ReviewRemarks,
    DateTime CreatedAt,
    DateTime? ApprovedAt,
    Guid? ConvertedTripId,
    IReadOnlyCollection<ShipmentRequestDocument> Documents,
    IReadOnlyCollection<DispatchShipmentRequestActivity> Activity,
    DispatchShipmentRequestAssignment? Assignment);

public sealed record CustomerShipmentListItem(
    Guid TripId,
    string? ContainerNumber,
    string PickupLocation,
    string DropoffLocation,
    TripStatus Status,
    DateTime? PickupTime,
    DateTime? DeliveredTime,
    TripDocumentState PodState);

public sealed record CustomerShipmentDetail(
    Guid TripId,
    string? ContainerNumber,
    TripStatus Status,
    string PickupLocation,
    string DropoffLocation,
    DateTime? PickupTime,
    DateTime? DropoffTime,
    DateTime? DeliveredTime,
    TripDocumentState PodState,
    TripDocumentState AtwState,
    bool WaybillGenerated,
    IReadOnlyCollection<CustomerShipmentStop> Stops);

public sealed record CustomerShipmentStop(
    TripStopType StopType,
    string LocationText,
    DateTime? ScheduledAt,
    DateTime? ActualAt);

public sealed record CustomerShipmentTimelineEntry(
    TripStatus FromStatus,
    TripStatus ToStatus,
    DateTime EventAt);

public sealed record CustomerShipmentDocument(
    TripDocumentType Type,
    TripDocumentState State,
    string StorageKey,
    DateTime UploadedAt);

public sealed class ShipmentRequestQueryService
{
    private readonly InventoryDbContext _dbContext;
    private readonly IDispatchShipmentReadService _dispatchShipmentReadService;

    public ShipmentRequestQueryService(
        InventoryDbContext dbContext,
        IDispatchShipmentReadService dispatchShipmentReadService)
    {
        _dbContext = dbContext;
        _dispatchShipmentReadService = dispatchShipmentReadService;
    }

    public async Task<PagedQueryResult<ShipmentRequestListItem>> GetCustomerRequestsAsync(
        Guid customerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ShipmentRequests
            .AsNoTracking()
            .Where(request => request.CustomerId == customerId);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(request => request.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(request => new ShipmentRequestListItem(
                request.Id,
                request.Status,
                request.PickupLocation,
                request.DropoffLocation,
                request.RequestedPickupTime,
                ToContainerSize(request.ContainerSize),
                ToTripType(request.TripType),
                request.ContainerNumber,
                request.ShippingLine,
                request.BookingNumber,
                _dbContext.ShipmentRequestDocuments.Count(doc => doc.RequestId == request.Id),
                request.CreatedAt,
                request.ApprovedAt,
                request.ConvertedTripId))
            .ToListAsync(cancellationToken);

        return new PagedQueryResult<ShipmentRequestListItem>(items, total);
    }

    public async Task<ShipmentRequestDetail> GetCustomerRequestDetailAsync(
        Guid requestId,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var request = await _dbContext.ShipmentRequests
            .AsNoTracking()
            .Include(r => r.Documents)
            .ThenInclude(doc => doc.UploadedByUser)
            .FirstOrDefaultAsync(r => r.Id == requestId && r.CustomerId == customerId, cancellationToken);

        if (request is null)
        {
            throw new NotFoundException("Shipment request not found.");
        }

        return new ShipmentRequestDetail(
            request.Id,
            request.Status,
            request.PickupLocation,
            request.DropoffLocation,
            request.RequestedPickupTime,
            ToContainerSize(request.ContainerSize),
            ToTripType(request.TripType),
            request.ContainerNumber,
            request.ShippingLine,
            request.BookingNumber,
            request.CargoDescription,
            request.CargoWeight,
            request.SpecialInstructions,
            request.RejectionRemarks,
            request.CreatedAt,
            request.ApprovedAt,
            request.ConvertedTripId,
            request.Documents.OrderByDescending(d => d.UploadedAt).ToList());
    }

    public async Task<PagedQueryResult<DispatchShipmentRequestQueueItem>> GetDispatchQueueAsync(
        int page,
        int pageSize,
        ShipmentRequestStatus? status = null,
        string? search = null,
        ShipmentRequestQueuePriority? priority = null,
        ShipmentRequestQueueAtwStatus? atwStatus = null,
        ShipmentRequestQueueSort sort = ShipmentRequestQueueSort.Priority,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ShipmentRequests
            .AsNoTracking()
            .Include(request => request.Customer)
            .AsQueryable();

        query = status.HasValue
            ? query.Where(request => request.Status == status.Value)
            : query.Where(request => request.Status == ShipmentRequestStatus.Submitted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(request =>
                (request.Customer != null && request.Customer.Name.Contains(term)) ||
                request.PickupLocation.Contains(term) ||
                request.DropoffLocation.Contains(term) ||
                (request.BookingNumber != null && request.BookingNumber.Contains(term)) ||
                (request.ContainerNumber != null && request.ContainerNumber.Contains(term)) ||
                (request.ShippingLine != null && request.ShippingLine.Contains(term)));
        }

        if (atwStatus == ShipmentRequestQueueAtwStatus.Uploaded)
        {
            query = query.Where(request => request.Documents.Any(document => document.DocumentType == ShipmentRequestDocumentType.Atw));
        }
        else if (atwStatus == ShipmentRequestQueueAtwStatus.Missing)
        {
            query = query.Where(request => !request.Documents.Any(document => document.DocumentType == ShipmentRequestDocumentType.Atw));
        }

        var now = DateTime.UtcNow;
        var criticalCutoff = now.AddHours(4);
        var highCutoff = now.AddHours(24);
        if (priority == ShipmentRequestQueuePriority.Critical)
        {
            query = query.Where(request => request.RequestedPickupTime.HasValue && request.RequestedPickupTime <= criticalCutoff);
        }
        else if (priority == ShipmentRequestQueuePriority.High)
        {
            query = query.Where(request => request.RequestedPickupTime > criticalCutoff && request.RequestedPickupTime <= highCutoff);
        }
        else if (priority == ShipmentRequestQueuePriority.Normal)
        {
            query = query.Where(request => !request.RequestedPickupTime.HasValue || request.RequestedPickupTime > highCutoff);
        }

        var total = await query.CountAsync(cancellationToken);

        var orderedQuery = sort switch
        {
            ShipmentRequestQueueSort.RequestedTime => query
                .OrderBy(request => request.RequestedPickupTime == null)
                .ThenBy(request => request.RequestedPickupTime)
                .ThenBy(request => request.CreatedAt),
            ShipmentRequestQueueSort.RequestedTimeDescending => query
                .OrderByDescending(request => request.RequestedPickupTime)
                .ThenBy(request => request.CreatedAt),
            ShipmentRequestQueueSort.Newest => query.OrderByDescending(request => request.CreatedAt),
            ShipmentRequestQueueSort.Oldest => query.OrderBy(request => request.CreatedAt),
            ShipmentRequestQueueSort.Customer => query
                .OrderBy(request => request.Customer != null ? request.Customer.Name : string.Empty)
                .ThenBy(request => request.CreatedAt),
            _ => query
                .OrderBy(request => !request.RequestedPickupTime.HasValue)
                .ThenBy(request => request.RequestedPickupTime)
                .ThenBy(request => request.CreatedAt)
        };

        var items = await orderedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(request => new DispatchShipmentRequestQueueItem(
                request.Id,
                request.CustomerId,
                request.Customer != null ? request.Customer.Name : string.Empty,
                request.PickupLocation,
                request.PickupLatitude,
                request.PickupLongitude,
                request.DropoffLocation,
                request.DropoffLatitude,
                request.DropoffLongitude,
                request.RequestedPickupTime,
                ToContainerSize(request.ContainerSize),
                ToTripType(request.TripType),
                request.ContainerNumber,
                request.ShippingLine,
                request.BookingNumber,
                _dbContext.ShipmentRequestDocuments.Count(doc => doc.RequestId == request.Id),
                request.Documents
                    .Where(document => document.DocumentType == ShipmentRequestDocumentType.Atw)
                    .OrderByDescending(document => document.UploadedAt)
                    .Select(document => (Guid?)document.Id)
                    .FirstOrDefault(),
                request.Documents
                    .Where(document => document.DocumentType == ShipmentRequestDocumentType.Atw)
                    .OrderByDescending(document => document.UploadedAt)
                    .Select(document => document.OriginalFileName)
                    .FirstOrDefault(),
                request.Documents
                    .Where(document => document.DocumentType == ShipmentRequestDocumentType.Atw)
                    .OrderByDescending(document => document.UploadedAt)
                    .Select(document => (DocumentAnalysisStatus?)document.AnalysisStatus)
                    .FirstOrDefault(),
                request.Documents
                    .Where(document => document.DocumentType == ShipmentRequestDocumentType.Atw)
                    .OrderByDescending(document => document.UploadedAt)
                    .Select(document => (DateTime?)document.UploadedAt)
                    .FirstOrDefault(),
                request.CreatedAt,
                request.Status,
                request.RequestedPickupTime.HasValue && request.RequestedPickupTime <= criticalCutoff
                    ? ShipmentRequestQueuePriority.Critical
                    : request.RequestedPickupTime.HasValue && request.RequestedPickupTime <= highCutoff
                        ? ShipmentRequestQueuePriority.High
                        : ShipmentRequestQueuePriority.Normal,
                request.RejectionRemarks))
            .ToListAsync(cancellationToken);

        return new PagedQueryResult<DispatchShipmentRequestQueueItem>(items, total);
    }

    public async Task<DispatchShipmentRequestDetail> GetDispatchRequestDetailAsync(
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        var request = await _dbContext.ShipmentRequests
            .AsNoTracking()
            .Include(item => item.Customer)
            .Include(item => item.Documents)
            .ThenInclude(document => document.UploadedByUser)
            .FirstOrDefaultAsync(item => item.Id == requestId, cancellationToken);

        if (request is null)
        {
            throw new NotFoundException("Shipment request not found.");
        }

        var activity = await _dbContext.AuditLogs
            .AsNoTracking()
            .Where(log => log.EntityType == "shipment_request" && log.EntityId == request.Id)
            .OrderByDescending(log => log.CreatedAt)
            .Select(log => new DispatchShipmentRequestActivity(
                log.Action,
                log.Actor != null ? log.Actor.Username : null,
                log.CreatedAt))
            .ToListAsync(cancellationToken);

        DispatchShipmentRequestAssignment? assignment = null;
        if (request.ConvertedTripId.HasValue)
        {
            assignment = await _dbContext.DispatchTrips
                .AsNoTracking()
                .Where(trip => trip.Id == request.ConvertedTripId.Value)
                .Select(trip => new DispatchShipmentRequestAssignment(
                    trip.Id,
                    trip.DriverUserId,
                    trip.Driver != null ? trip.Driver.Username : null,
                    trip.TruckAssetId,
                    trip.TruckAsset != null ? trip.TruckAsset.AssetCode : null,
                    trip.TrailerAssetId,
                    trip.TrailerAsset != null ? trip.TrailerAsset.AssetCode : null))
                .FirstOrDefaultAsync(cancellationToken);
        }

        return new DispatchShipmentRequestDetail(
            request.Id,
            request.CustomerId,
            request.Customer?.Name ?? string.Empty,
            request.Status,
            request.PickupLocation,
            request.PickupLatitude,
            request.PickupLongitude,
            request.DropoffLocation,
            request.DropoffLatitude,
            request.DropoffLongitude,
            request.RequestedPickupTime,
            ToContainerSize(request.ContainerSize),
            ToTripType(request.TripType),
            request.ContainerNumber,
            request.ShippingLine,
            request.BookingNumber,
            request.CargoDescription,
            request.CargoWeight,
            request.SpecialInstructions,
            request.RejectionRemarks,
            request.CreatedAt,
            request.ApprovedAt,
            request.ConvertedTripId,
            request.Documents.OrderByDescending(document => document.UploadedAt).ToList(),
            activity,
            assignment);
    }

    public async Task<PagedQueryResult<CustomerShipmentListItem>> GetCustomerShipmentsAsync(
        Guid customerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await _dispatchShipmentReadService.GetCustomerShipmentsAsync(
            customerId,
            page,
            pageSize,
            cancellationToken);

        return new PagedQueryResult<CustomerShipmentListItem>(
            result.Items.Select(MapListItem).ToList(),
            result.TotalCount);
    }

    public async Task<CustomerShipmentDetail> GetCustomerShipmentDetailAsync(
        Guid tripId,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var detail = await _dispatchShipmentReadService.GetCustomerShipmentDetailAsync(
            tripId,
            customerId,
            cancellationToken);

        return MapDetail(detail);
    }

    public async Task<IReadOnlyCollection<CustomerShipmentTimelineEntry>> GetCustomerShipmentTimelineAsync(
        Guid tripId,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var entries = await _dispatchShipmentReadService.GetCustomerShipmentTimelineAsync(
            tripId,
            customerId,
            cancellationToken);
        return entries.Select(MapTimelineEntry).ToList();
    }

    public async Task<IReadOnlyCollection<CustomerShipmentDocument>> GetCustomerShipmentDocumentsAsync(
        Guid tripId,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var docs = await _dispatchShipmentReadService.GetCustomerShipmentDocumentsAsync(
            tripId,
            customerId,
            cancellationToken);
        return docs.Select(MapDocument).ToList();
    }

    private static CustomerShipmentListItem MapListItem(DispatchCustomerShipmentListItem item)
    {
        return new CustomerShipmentListItem(
            item.TripId,
            item.ContainerNumber,
            item.PickupLocation,
            item.DropoffLocation,
            item.Status,
            item.PickupTime,
            item.DeliveredTime,
            item.PodState);
    }

    private static CustomerShipmentDetail MapDetail(DispatchCustomerShipmentDetail detail)
    {
        return new CustomerShipmentDetail(
            detail.TripId,
            detail.ContainerNumber,
            detail.Status,
            detail.PickupLocation,
            detail.DropoffLocation,
            detail.PickupTime,
            detail.DropoffTime,
            detail.DeliveredTime,
            detail.PodState,
            detail.AtwState,
            detail.WaybillGenerated,
            detail.Stops.Select(stop => new CustomerShipmentStop(
                stop.StopType,
                stop.LocationText,
                stop.ScheduledAt,
                stop.ActualAt)).ToList());
    }

    private static CustomerShipmentTimelineEntry MapTimelineEntry(DispatchCustomerShipmentTimelineEntry entry)
    {
        return new CustomerShipmentTimelineEntry(
            entry.FromStatus,
            entry.ToStatus,
            entry.EventAt);
    }

    private static CustomerShipmentDocument MapDocument(DispatchCustomerShipmentDocument document)
    {
        return new CustomerShipmentDocument(
            document.Type,
            document.State,
            document.StorageKey,
            document.UploadedAt);
    }

    private static ContainerSize ToContainerSize(string? value)
    {
        return NormalizeStorageValue(value) switch
        {
            "FORTYFT" or "FORTY_FT" => ContainerSize.FortyFt,
            "FORTYHC" or "FORTY_HC" => ContainerSize.FortyHC,
            "TWENTYFT" or "TWENTY_FT" => ContainerSize.TwentyFt,
            _ => ContainerSize.TwentyFt
        };
    }

    private static TripType ToTripType(string? value)
    {
        return NormalizeStorageValue(value) switch
        {
            "PORT_DROPOFF" => TripType.PortDropoff,
            "YARD_TRANSFER" => TripType.YardTransfer,
            "LONG_HAUL" => TripType.LongHaul,
            "PORTPICKUP" or "PORT_PICKUP" => TripType.PortPickup,
            _ => TripType.PortPickup
        };
    }

    private static string NormalizeStorageValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().Replace("-", "_", StringComparison.Ordinal).ToUpperInvariant();
    }
}
