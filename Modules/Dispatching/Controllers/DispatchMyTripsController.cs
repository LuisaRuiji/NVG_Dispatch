using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NVGInventory.Contracts;
using NVGInventory.Domain.Constants;
using NVGInventory.Security;
using NVGInventory.Modules.Dispatching;
using NVGInventory.Modules.Dispatching.Services;

namespace NVGInventory.Modules.Dispatching.Controllers;

[ApiController]
[Route("api/dispatch/my-trips")]
[Authorize(Roles = RoleNames.Driver)]
public sealed class DispatchMyTripsController : ControllerBase
{
    private readonly DispatchTripQueryService _queryService;
    private readonly DispatchingOptions _options;

    public DispatchMyTripsController(
        DispatchTripQueryService queryService,
        IOptions<DispatchingOptions> options)
    {
        _queryService = queryService;
        _options = options.Value ?? new DispatchingOptions();
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<DispatchTripListItemResponse>>> GetMyTrips(
        [FromQuery] string? scope,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var normalizedScope = string.IsNullOrWhiteSpace(scope) ? "ACTIVE" : scope.Trim();
        if (!normalizedScope.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase) &&
            !normalizedScope.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Invalid scope. Use ACTIVE or ALL.");
        }

        var resolvedPage = page.GetValueOrDefault(1);
        if (resolvedPage < 1)
        {
            return BadRequest("Page must be at least 1.");
        }

        var resolvedPageSize = pageSize.GetValueOrDefault(20);
        if (resolvedPageSize < 1 || resolvedPageSize > 100)
        {
            return BadRequest("Page size must be between 1 and 100.");
        }

        var includeClosed = normalizedScope.Equals("ALL", StringComparison.OrdinalIgnoreCase);

        var results = await _queryService.GetMyTripsAsync(
            User.GetUserId(),
            includeClosed,
            from,
            to,
            resolvedPage,
            resolvedPageSize,
            cancellationToken);

        var responseItems = results.Items
            .Select(item => new DispatchTripListItemResponse(
                item.Id,
                item.Status,
                new DispatchCustomerSummaryResponse(item.CustomerId, item.CustomerName),
                item.ContainerNumber,
                item.DriverUserId,
                item.DriverUsername,
                item.TruckAssetId,
                item.TruckAssetCode,
                item.PodPending,
                item.UploadedDocumentCount,
                item.RequiredDocumentCount,
                item.Documents.Select(doc => new DispatchTripDocumentChecklistResponse(
                    doc.Type,
                    doc.State)).ToList(),
                item.PodState,
                item.CloseDocumentReady,
                item.MissingRequiredDocumentCount,
                item.RejectedRequiredDocumentCount,
                item.CloseDocumentBlockReason,
                item.CreatedAt,
                item.UpdatedAt,
                item.PickupLocation,
                item.DropoffLocation,
                item.PickupScheduledAt,
                item.DropoffScheduledAt,
                item.PlannedStart,
                item.PlannedEnd,
                item.PlannedDurationMinutes,
                item.LatePickup,
                item.LateDelivery,
                item.OnHoldMinutes,
                Convert.ToBase64String(item.RowVersion),
                item.PickupLatitude,
                item.PickupLongitude,
                item.DropoffLatitude,
                item.DropoffLongitude,
                MapLatestLocation(item.LatestDriverLocation)))
            .ToList();

        return Ok(new PagedResult<DispatchTripListItemResponse>(
            responseItems,
            results.TotalCount,
            resolvedPage,
            resolvedPageSize));
    }

    [HttpGet("{tripId:guid}")]
    public async Task<ActionResult<DispatchTripDetailResponse>> GetMyTrip(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        var detail = await _queryService.GetMyTripDetailAsync(tripId, User.GetUserId(), cancellationToken);

        var response = new DispatchTripDetailResponse(
            detail.Id,
            detail.Status,
            new DispatchCustomerSummaryResponse(detail.CustomerId, detail.CustomerName),
            detail.DriverUserId,
            detail.DriverUsername,
            detail.TruckAssetId,
            detail.TruckAssetCode,
            detail.PodPending,
            detail.HoldPreviousStatus,
            detail.Notes,
            detail.ContainerNumber,
            detail.EirNumber,
            detail.BookingNumber,
            detail.ShippingLine,
            detail.CreatedAt,
            detail.UpdatedAt,
            detail.Stops.Select(stop => new DispatchTripStopResponse(
                stop.Id,
                stop.StopType,
                stop.LocationText,
                stop.ScheduledAt,
                stop.ActualAt,
                stop.Latitude,
                stop.Longitude)).ToList(),
            detail.Documents.Select(doc => new DispatchTripDocumentResponse(
                doc.Id,
                doc.Type,
                doc.State,
                doc.StorageKey,
                doc.UploadedByUserId,
                doc.UploadedBy?.Username,
                doc.VerifiedByUserId,
                doc.VerifiedBy?.Username,
                doc.RejectedByUserId,
                doc.RejectedBy?.Username,
                doc.Remarks,
                doc.UploadedAt,
                doc.VerifiedAt,
                doc.RejectedAt)).ToList(),
            detail.History.Select(entry => new DispatchTripHistoryResponse(
                entry.Id,
                entry.EventType,
                entry.FromStatus,
                entry.ToStatus,
                entry.ActorUserId,
                entry.Actor?.Username,
                entry.Remarks,
                entry.EventAt,
                entry.RecordedAt)).ToList(),
            _options.DocVerificationEnabled,
            Convert.ToBase64String(detail.RowVersion),
            LatestDriverLocation: MapLatestLocation(detail.LatestDriverLocation));

        return Ok(response);
    }

    private static DispatchTripLatestDriverLocationResponse? MapLatestLocation(DispatchTripLatestDriverLocation? location)
    {
        return location is null
            ? null
            : new DispatchTripLatestDriverLocationResponse(
                location.Latitude,
                location.Longitude,
                location.AccuracyMeters,
                location.RecordedAt,
                location.IsStale);
    }
}
