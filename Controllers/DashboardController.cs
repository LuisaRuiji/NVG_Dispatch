using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Enums;
using NVGInventory.Domain.Services;
using NVGInventory.Modules.Dispatching;
using NVGInventory.Modules.Dispatching.Enums;
using NVGInventory.Modules.ShipmentRequests.Enums;
using NVGInventory.Security;
using NVGInventory.Services;

namespace NVGInventory.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public sealed class DashboardController : ControllerBase
{
    private static readonly TripStatus[] ActiveOperationalStatuses =
    [
        TripStatus.Dispatched,
        TripStatus.EnroutePickup,
        TripStatus.AtPickup,
        TripStatus.Loaded,
        TripStatus.EnrouteDropoff,
        TripStatus.AtDropoff,
        TripStatus.OnHold,
        TripStatus.FailedAttempt
    ];

    private static readonly TripStatus[] ActiveOrDeliveredStatuses =
    [
        TripStatus.Dispatched,
        TripStatus.EnroutePickup,
        TripStatus.AtPickup,
        TripStatus.Loaded,
        TripStatus.EnrouteDropoff,
        TripStatus.AtDropoff,
        TripStatus.OnHold,
        TripStatus.FailedAttempt,
        TripStatus.Delivered,
        TripStatus.Closed
    ];

    private readonly InventoryDbContext _dbContext;
    private readonly DispatchingOptions _dispatchingOptions;
    private readonly IAuditService _auditService;
    private readonly IVaiaCacheService _cacheService;

    public DashboardController(
        InventoryDbContext dbContext,
        IOptions<DispatchingOptions> dispatchingOptions,
        IAuditService auditService,
        IVaiaCacheService cacheService)
    {
        _dbContext = dbContext;
        _dispatchingOptions = dispatchingOptions.Value ?? new DispatchingOptions();
        _auditService = auditService;
        _cacheService = cacheService;
    }

    [HttpGet("dispatch-kpis")]
    [Authorize(Roles = $"{RoleNames.Dispatcher},{RoleNames.Manager},{RoleNames.Admin},{RoleNames.SuperAdmin},{RoleNames.Ceo}")]
    public async Task<ActionResult<DispatchDashboardKpisResponse>> GetDispatchKpis(CancellationToken cancellationToken)
    {
        var response = await _cacheService.GetOrSetAsync(
            VaiaCacheKeys.DispatchKpis,
            async () =>
            {
                var activeTripIds = await _dbContext.DispatchTrips
                    .AsNoTracking()
                    .Where(trip => ActiveOperationalStatuses.Contains(trip.Status))
                    .Select(trip => trip.Id)
                    .ToListAsync(cancellationToken);

                var driversOnRoad = await _dbContext.DispatchTrips
                    .AsNoTracking()
                    .Where(trip => ActiveOperationalStatuses.Contains(trip.Status) && trip.DriverUserId.HasValue)
                    .Select(trip => trip.DriverUserId!.Value)
                    .Distinct()
                    .CountAsync(cancellationToken);

                var totalActiveDrivers = await _dbContext.Users
                    .AsNoTracking()
                    .Where(user => user.IsActive)
                    .Where(user => user.UserRoles.Any(userRole => userRole.Role != null && userRole.Role.Name == RoleNames.Driver))
                    .CountAsync(cancellationToken);

                var documentAlerts = await _dbContext.DispatchTripDocuments
                    .AsNoTracking()
                    .Where(document => document.IsActive && document.State == TripDocumentState.Uploaded)
                    .CountAsync(cancellationToken);

                var submittedShipmentRequests = await _dbContext.ShipmentRequests
                    .AsNoTracking()
                    .CountAsync(request => request.Status == ShipmentRequestStatus.Submitted, cancellationToken);

                var approvedShipmentRequests = await _dbContext.ShipmentRequests
                    .AsNoTracking()
                    .CountAsync(request => request.Status == ShipmentRequestStatus.Approved, cancellationToken);

                var incompleteDocumentAlerts = await CountMissingRequiredDocumentsAsync(activeTripIds, cancellationToken);
                var statusBreakdown = await _dbContext.DispatchTrips
                    .AsNoTracking()
                    .Where(trip => trip.Status != TripStatus.Closed && trip.Status != TripStatus.Cancelled)
                    .GroupBy(trip => trip.Status)
                    .Select(group => new StatusCountChartItemResponse(group.Key.ToString(), group.Count()))
                    .ToListAsync(cancellationToken);

                return new DispatchDashboardKpisResponse(
                    activeTripIds.Count,
                    driversOnRoad,
                    Math.Max(totalActiveDrivers - driversOnRoad, 0),
                    documentAlerts,
                    submittedShipmentRequests,
                    await _dbContext.DispatchTrips.AsNoTracking().CountAsync(trip => trip.Status == TripStatus.OnHold, cancellationToken),
                    await _dbContext.DispatchTrips.AsNoTracking().CountAsync(trip => trip.Status == TripStatus.Draft, cancellationToken),
                    await _dbContext.DispatchTrips.AsNoTracking().CountAsync(trip => trip.Status == TripStatus.FailedAttempt, cancellationToken),
                    approvedShipmentRequests,
                    incompleteDocumentAlerts,
                    statusBreakdown);
            },
            TimeSpan.FromMinutes(2),
            cancellationToken);
        return Ok(response);
    }

    [HttpGet("dispatcher-weekly-trips")]
    [Authorize(Roles = $"{RoleNames.Dispatcher},{RoleNames.Manager},{RoleNames.Admin},{RoleNames.SuperAdmin}")]
    public async Task<ActionResult<IReadOnlyCollection<TimeCountChartItemResponse>>> GetDispatcherWeeklyTrips(
        CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        var weekStart = GetUtcWeekStart(today);
        var nextWeekStart = weekStart.AddDays(7);
        var trips = await _dbContext.DispatchTrips
            .AsNoTracking()
            .Where(trip => trip.CreatedAt >= weekStart && trip.CreatedAt < nextWeekStart)
            .Select(trip => trip.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(Enumerable.Range(0, 7)
            .Select(offset =>
            {
                var day = weekStart.AddDays(offset);
                return new TimeCountChartItemResponse(
                    day.ToString("ddd"),
                    trips.Count(createdAt => createdAt.Date == day));
            })
            .ToList());
    }

    [HttpGet("document-alerts")]
    [Authorize(Roles = $"{RoleNames.Dispatcher},{RoleNames.Manager},{RoleNames.Admin},{RoleNames.SuperAdmin}")]
    public async Task<ActionResult<IReadOnlyCollection<DocumentAlertChartItemResponse>>> GetDocumentAlerts(
        CancellationToken cancellationToken)
    {
        var grouped = await _dbContext.DispatchTripDocuments
            .AsNoTracking()
            .Where(document => document.IsActive && document.State == TripDocumentState.Uploaded)
            .GroupBy(document => document.Type)
            .Select(group => new DocumentAlertChartItemResponse(group.Key.ToString(), group.Count()))
            .ToListAsync(cancellationToken);

        return Ok(WithDocumentTypes(grouped));
    }

    [HttpGet("finance-kpis")]
    [Authorize(Roles = $"{RoleNames.HeadOfFinance},{RoleNames.Manager},{RoleNames.Admin},{RoleNames.SuperAdmin},{RoleNames.Ceo}")]
    public async Task<ActionResult<FinanceDashboardKpisResponse>> GetFinanceKpis(CancellationToken cancellationToken)
    {
        var response = await _cacheService.GetOrSetAsync(
            VaiaCacheKeys.FinanceKpis,
            async () =>
            {
                var (monthStart, nextMonthStart) = GetCurrentUtcMonthRange();

                var activeOrDeliveredFinancials = await _dbContext.DispatchTrips
                    .AsNoTracking()
                    .Where(trip => trip.CreatedAt >= monthStart && trip.CreatedAt < nextMonthStart)
                    .Where(trip => ActiveOrDeliveredStatuses.Contains(trip.Status))
                    .Select(trip => new TripFinancialProjection(trip.Rate, trip.Payroll))
                    .ToListAsync(cancellationToken);

                var deliveredThisMonthTripIds = await GetDeliveredThisMonthTripIdsAsync(monthStart, nextMonthStart, cancellationToken);

                var deliveredFinancials = deliveredThisMonthTripIds.Count == 0
                    ? new List<TripFinancialProjection>()
                    : await _dbContext.DispatchTrips
                        .AsNoTracking()
                        .Where(trip => deliveredThisMonthTripIds.Contains(trip.Id))
                        .Select(trip => new TripFinancialProjection(trip.Rate, trip.Payroll))
                        .ToListAsync(cancellationToken);

                return new FinanceDashboardKpisResponse(
                    activeOrDeliveredFinancials.Sum(item => item.Rate ?? 0m),
                    deliveredFinancials.Sum(item => item.Payroll ?? 0m),
                    await _dbContext.PurchaseOrders.AsNoTracking().CountAsync(po => po.Status == PurchaseOrderStatus.PendingFinance, cancellationToken),
                    activeOrDeliveredFinancials.Count(item => item.Rate is null));
            },
            TimeSpan.FromMinutes(2),
            cancellationToken);

        await RecordFinancialAccessAsync("dashboard/finance-kpis", ["Rate", "Payroll"], cancellationToken);

        return Ok(response);
    }

    [HttpGet("driver-kpis")]
    [Authorize(Roles = RoleNames.Driver)]
    public async Task<ActionResult<DriverDashboardKpisResponse>> GetDriverKpis(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var response = await _cacheService.GetOrSetAsync(
            VaiaCacheKeys.DriverKpis(userId),
            async () =>
            {
                var today = DateTime.UtcNow.Date;
                var tomorrow = today.AddDays(1);
                var weekStart = GetUtcWeekStart(today);

                var activeTrip = await _dbContext.DispatchTrips
                    .AsNoTracking()
                    .Include(trip => trip.Customer)
                    .Include(trip => trip.TruckAsset)
                    .Where(trip => trip.DriverUserId == userId)
                    .Where(trip => ActiveOperationalStatuses.Contains(trip.Status))
                    .OrderByDescending(trip => trip.CreatedAt)
                    .Select(trip => new DriverActiveTripKpiResponse(
                        trip.Id,
                        trip.Status,
                        trip.Customer != null ? trip.Customer.Name : null,
                        trip.TruckAsset != null ? trip.TruckAsset.AssetCode : null))
                    .FirstOrDefaultAsync(cancellationToken);

                var activeTripIds = await _dbContext.DispatchTrips
                    .AsNoTracking()
                    .Where(trip => trip.DriverUserId == userId)
                    .Where(trip => trip.Status != TripStatus.Closed && trip.Status != TripStatus.Cancelled)
                    .Select(trip => trip.Id)
                    .ToListAsync(cancellationToken);

                var weekTripDates = await _dbContext.DispatchTrips
                    .AsNoTracking()
                    .Where(trip => trip.DriverUserId == userId)
                    .Where(trip => trip.CreatedAt >= weekStart && trip.CreatedAt < weekStart.AddDays(7))
                    .Select(trip => trip.CreatedAt)
                    .ToListAsync(cancellationToken);

                var thirtyDaysAgo = today.AddDays(-30);
                var recentDeliveredTrips = await _dbContext.DispatchTrips
                    .AsNoTracking()
                    .Where(trip => trip.DriverUserId == userId)
                    .Where(trip => trip.CreatedAt >= thirtyDaysAgo)
                    .Where(trip => trip.Status == TripStatus.Delivered || trip.Status == TripStatus.Closed)
                    .Select(trip => new DriverRecentTripProjection(trip.Id, trip.CreatedAt, trip.UpdatedAt))
                    .ToListAsync(cancellationToken);
                var recentTripIds = recentDeliveredTrips.Select(trip => trip.Id).ToList();
                var recentCompliantTripIds = await GetDocumentCompliantTripIdsAsync(recentTripIds, cancellationToken);
                var onTimeTrips = recentDeliveredTrips.Count(IsDeliveredOnTime);

                return new DriverDashboardKpisResponse(
                    activeTrip,
                    await _dbContext.DispatchTrips.AsNoTracking().CountAsync(
                        trip => trip.DriverUserId == userId && ((trip.CreatedAt >= today && trip.CreatedAt < tomorrow) || ActiveOperationalStatuses.Contains(trip.Status)),
                        cancellationToken),
                    await _dbContext.DispatchTrips.AsNoTracking().CountAsync(
                        trip => trip.DriverUserId == userId && (trip.CreatedAt >= weekStart || ActiveOperationalStatuses.Contains(trip.Status)),
                        cancellationToken),
                    await CountMissingRequiredDocumentsAsync(activeTripIds, cancellationToken),
                    Enumerable.Range(0, 7)
                        .Select(offset =>
                        {
                            var day = weekStart.AddDays(offset);
                            return new TimeCountChartItemResponse(
                                day.ToString("ddd"),
                                weekTripDates.Count(createdAt => createdAt.Date == day));
                        })
                        .ToList(),
                    CalculatePercent(onTimeTrips, recentDeliveredTrips.Count),
                    CalculatePercent(recentCompliantTripIds.Count, recentDeliveredTrips.Count));
            },
            TimeSpan.FromMinutes(2),
            cancellationToken);

        return Ok(response);
    }

    [HttpGet("customer-kpis")]
    [Authorize(Roles = RoleNames.Customer)]
    public async Task<ActionResult<CustomerDashboardKpisResponse>> GetCustomerKpis(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var customerId = await GetCurrentCustomerIdAsync(cancellationToken);
        if (!customerId.HasValue)
        {
            return Ok(new CustomerDashboardKpisResponse(0, [], 0, 0, []));
        }

        var response = await _cacheService.GetOrSetAsync(
            VaiaCacheKeys.CustomerKpis(userId),
            async () =>
            {
                var activeRequestStatuses = new[]
                {
                    ShipmentRequestStatus.Draft,
                    ShipmentRequestStatus.Submitted,
                    ShipmentRequestStatus.Approved
                };

                var activeRequestStatusGroups = await _dbContext.ShipmentRequests
                    .AsNoTracking()
                    .Where(request => request.CustomerId == customerId.Value)
                    .Where(request => activeRequestStatuses.Contains(request.Status))
                    .GroupBy(request => request.Status)
                    .Select(group => new CustomerActiveRequestStatusKpiResponse(group.Key, group.Count()))
                    .ToListAsync(cancellationToken);

                var (monthStart, nextMonthStart) = GetCurrentUtcMonthRange();
                var deliveredThisMonthTripIds = await GetDeliveredThisMonthTripIdsAsync(monthStart, nextMonthStart, cancellationToken);
                var deliveredThisMonth = deliveredThisMonthTripIds.Count == 0
                    ? 0
                    : await _dbContext.DispatchTrips
                        .AsNoTracking()
                        .CountAsync(trip => trip.CustomerId == customerId.Value && deliveredThisMonthTripIds.Contains(trip.Id), cancellationToken);

                return new CustomerDashboardKpisResponse(
                    activeRequestStatusGroups.Sum(group => group.Count),
                    activeRequestStatusGroups,
                    deliveredThisMonth,
                    await _dbContext.ShipmentRequests
                        .AsNoTracking()
                        .Where(request => request.CustomerId == customerId.Value)
                        .Where(request => request.Status == ShipmentRequestStatus.Draft || request.Status == ShipmentRequestStatus.Submitted)
                        .CountAsync(request => !_dbContext.ShipmentRequestDocuments.Any(document => document.RequestId == request.Id), cancellationToken),
                    activeRequestStatusGroups
                        .Select(group => new StatusCountChartItemResponse(group.Status.ToString(), group.Count))
                        .ToList());
            },
            TimeSpan.FromMinutes(2),
            cancellationToken);

        return Ok(response);
    }

    [HttpGet("system-kpis")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.SuperAdmin},{RoleNames.Ceo},{RoleNames.Manager}")]
    public async Task<ActionResult<SystemDashboardKpisResponse>> GetSystemKpis(CancellationToken cancellationToken)
    {
        var response = await _cacheService.GetOrSetAsync(
            VaiaCacheKeys.SystemKpis,
            async () =>
            {
                var (monthStart, nextMonthStart) = GetCurrentUtcMonthRange();
                var totalActiveTrucks = await _dbContext.Assets
                    .AsNoTracking()
                    .CountAsync(asset => asset.AssetType == AssetType.Truck && asset.Status == AssetStatus.Active, cancellationToken);

                var activeTruckCount = await _dbContext.DispatchTrips
                    .AsNoTracking()
                    .Where(trip => ActiveOperationalStatuses.Contains(trip.Status) && trip.TruckAssetId.HasValue)
                    .Select(trip => trip.TruckAssetId!.Value)
                    .Distinct()
                    .CountAsync(cancellationToken);

                return new SystemDashboardKpisResponse(
                    await _dbContext.Users.AsNoTracking().CountAsync(cancellationToken),
                    await _dbContext.DispatchTrips.AsNoTracking().CountAsync(
                        trip => trip.CreatedAt >= monthStart && trip.CreatedAt < nextMonthStart,
                        cancellationToken),
                    CalculatePercent(activeTruckCount, totalActiveTrucks),
                    await CalculateDocumentComplianceRateAsync(cancellationToken),
                    await GetMonthlyTripVolumeAsync(6, cancellationToken),
                    await GetActiveUsersByRoleAsync(cancellationToken));
            },
            TimeSpan.FromMinutes(2),
            cancellationToken);

        return Ok(response);
    }

    [HttpGet("manager-weekly-completion")]
    [Authorize(Roles = $"{RoleNames.Manager},{RoleNames.Admin},{RoleNames.SuperAdmin},{RoleNames.Ceo}")]
    public async Task<ActionResult<IReadOnlyCollection<PercentByPeriodChartItemResponse>>> GetManagerWeeklyCompletion(
        CancellationToken cancellationToken)
    {
        var weekStarts = GetRecentWeekStarts(8);
        var from = weekStarts[0];
        var to = weekStarts[^1].AddDays(7);
        var trips = await _dbContext.DispatchTrips
            .AsNoTracking()
            .Where(trip => trip.CreatedAt >= from && trip.CreatedAt < to)
            .Where(trip => trip.Status != TripStatus.Cancelled)
            .Select(trip => new TripStatusCreatedProjection(trip.Status, trip.CreatedAt))
            .ToListAsync(cancellationToken);

        return Ok(weekStarts
            .Select(weekStart =>
            {
                var weekTrips = trips
                    .Where(trip => trip.CreatedAt >= weekStart && trip.CreatedAt < weekStart.AddDays(7))
                    .ToList();
                var delivered = weekTrips.Count(trip => trip.Status is TripStatus.Delivered or TripStatus.Closed);
                return new PercentByPeriodChartItemResponse(FormatWeekLabel(weekStart), CalculatePercent(delivered, weekTrips.Count));
            })
            .ToList());
    }

    [HttpGet("driver-utilization")]
    [Authorize(Roles = $"{RoleNames.Manager},{RoleNames.Admin},{RoleNames.SuperAdmin},{RoleNames.Ceo}")]
    public async Task<ActionResult<IReadOnlyCollection<DriverUtilizationChartItemResponse>>> GetDriverUtilization(
        CancellationToken cancellationToken)
    {
        var (monthStart, nextMonthStart) = GetCurrentUtcMonthRange();
        var tripCounts = await _dbContext.DispatchTrips
            .AsNoTracking()
            .Where(trip => trip.CreatedAt >= monthStart && trip.CreatedAt < nextMonthStart)
            .Where(trip => trip.DriverUserId.HasValue)
            .GroupBy(trip => trip.DriverUserId!.Value)
            .Select(group => new { DriverUserId = group.Key, Trips = group.Count() })
            .OrderByDescending(item => item.Trips)
            .Take(12)
            .ToListAsync(cancellationToken);

        var driverIds = tripCounts.Select(item => item.DriverUserId).ToList();
        var driverNames = await _dbContext.Users
            .AsNoTracking()
            .Where(user => driverIds.Contains(user.Id))
            .Select(user => new { user.Id, user.Username })
            .ToDictionaryAsync(user => user.Id, user => user.Username, cancellationToken);

        return Ok(tripCounts
            .Select(item => new DriverUtilizationChartItemResponse(
                driverNames.GetValueOrDefault(item.DriverUserId, "Unassigned"),
                item.Trips))
            .ToList());
    }

    [HttpGet("ontime-vs-delayed")]
    [Authorize(Roles = $"{RoleNames.Manager},{RoleNames.Admin},{RoleNames.SuperAdmin},{RoleNames.Ceo}")]
    public async Task<ActionResult<IReadOnlyCollection<OnTimeDelayedChartItemResponse>>> GetOnTimeVsDelayed(
        CancellationToken cancellationToken)
    {
        var weekStarts = GetRecentWeekStarts(8);
        var from = weekStarts[0];
        var to = weekStarts[^1].AddDays(7);
        var deliveredTrips = await _dbContext.DispatchTrips
            .AsNoTracking()
            .Where(trip => trip.CreatedAt >= from && trip.CreatedAt < to)
            .Where(trip => trip.Status == TripStatus.Delivered || trip.Status == TripStatus.Closed)
            .Select(trip => new TripDeliveryTimingProjection(trip.CreatedAt, trip.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Ok(weekStarts
            .Select(weekStart =>
            {
                var weekTrips = deliveredTrips
                    .Where(trip => trip.CreatedAt >= weekStart && trip.CreatedAt < weekStart.AddDays(7))
                    .ToList();
                var onTime = weekTrips.Count(IsDeliveredOnTime);
                return new OnTimeDelayedChartItemResponse(FormatWeekLabel(weekStart), onTime, Math.Max(weekTrips.Count - onTime, 0));
            })
            .ToList());
    }

    [HttpGet("finance-weekly-revenue")]
    [Authorize(Roles = $"{RoleNames.HeadOfFinance},{RoleNames.Manager},{RoleNames.Admin},{RoleNames.SuperAdmin},{RoleNames.Ceo}")]
    public async Task<ActionResult<IReadOnlyCollection<FinancialRevenueChartItemResponse>>> GetFinanceWeeklyRevenue(
        CancellationToken cancellationToken)
    {
        var weekStarts = GetRecentWeekStarts(8);
        var rows = await GetFinancialBreakdownAsync(weekStarts[0], weekStarts[^1].AddDays(7), weekStarts, cancellationToken);
        await RecordFinancialAccessAsync("dashboard/finance-weekly-revenue", ["Rate"], cancellationToken);
        return Ok(rows.Select(row => new FinancialRevenueChartItemResponse(row.Period, row.Revenue)).ToList());
    }

    [HttpGet("finance-weekly-breakdown")]
    [Authorize(Roles = $"{RoleNames.HeadOfFinance},{RoleNames.Manager},{RoleNames.Admin},{RoleNames.SuperAdmin},{RoleNames.Ceo}")]
    public async Task<ActionResult<IReadOnlyCollection<FinancialBreakdownChartItemResponse>>> GetFinanceWeeklyBreakdown(
        [FromQuery] string? groupBy,
        CancellationToken cancellationToken)
    {
        var useMonth = string.Equals(groupBy, "month", StringComparison.OrdinalIgnoreCase);
        var starts = useMonth ? GetRecentMonthStarts(6) : GetRecentWeekStarts(8);
        var end = useMonth ? starts[^1].AddMonths(1) : starts[^1].AddDays(7);
        var rows = await GetFinancialBreakdownAsync(starts[0], end, starts, cancellationToken);
        await RecordFinancialAccessAsync("dashboard/finance-weekly-breakdown", ["Rate", "Payroll", "FuelAmount", "FuelPricePerLiter"], cancellationToken);
        return Ok(rows);
    }

    [HttpGet("customer-monthly-deliveries")]
    [Authorize(Roles = RoleNames.Customer)]
    public async Task<ActionResult<IReadOnlyCollection<TimeCountChartItemResponse>>> GetCustomerMonthlyDeliveries(
        CancellationToken cancellationToken)
    {
        var customerId = await GetCurrentCustomerIdAsync(cancellationToken);
        if (!customerId.HasValue)
        {
            return Ok(Array.Empty<TimeCountChartItemResponse>());
        }

        var monthStarts = GetRecentMonthStarts(6);
        var from = monthStarts[0];
        var to = monthStarts[^1].AddMonths(1);
        var trips = await _dbContext.DispatchTrips
            .AsNoTracking()
            .Where(trip => trip.CustomerId == customerId.Value)
            .Where(trip => trip.CreatedAt >= from && trip.CreatedAt < to)
            .Where(trip => trip.Status == TripStatus.Delivered || trip.Status == TripStatus.Closed)
            .Select(trip => trip.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(monthStarts
            .Select(monthStart => new TimeCountChartItemResponse(
                FormatMonthLabel(monthStart),
                trips.Count(createdAt => createdAt >= monthStart && createdAt < monthStart.AddMonths(1))))
            .ToList());
    }

    [HttpGet("ceo-fleet-utilization")]
    [Authorize(Roles = $"{RoleNames.Ceo},{RoleNames.Admin},{RoleNames.SuperAdmin}")]
    public async Task<ActionResult<IReadOnlyCollection<PercentByPeriodChartItemResponse>>> GetCeoFleetUtilization(
        CancellationToken cancellationToken)
    {
        var weekStarts = GetRecentWeekStarts(8);
        var from = weekStarts[0];
        var to = weekStarts[^1].AddDays(7);
        var totalActiveTrucks = await _dbContext.Assets
            .AsNoTracking()
            .CountAsync(asset => asset.AssetType == AssetType.Truck && asset.Status == AssetStatus.Active, cancellationToken);
        var trips = await _dbContext.DispatchTrips
            .AsNoTracking()
            .Where(trip => trip.CreatedAt >= from && trip.CreatedAt < to)
            .Where(trip => trip.TruckAssetId.HasValue)
            .Select(trip => new TripTruckCreatedProjection(trip.CreatedAt, trip.TruckAssetId!.Value))
            .ToListAsync(cancellationToken);

        return Ok(weekStarts
            .Select(weekStart =>
            {
                var trucks = trips
                    .Where(trip => trip.CreatedAt >= weekStart && trip.CreatedAt < weekStart.AddDays(7))
                    .Select(trip => trip.TruckAssetId)
                    .Distinct()
                    .Count();
                return new PercentByPeriodChartItemResponse(FormatWeekLabel(weekStart), CalculatePercent(trucks, totalActiveTrucks));
            })
            .ToList());
    }

    [HttpGet("ceo-trip-volume")]
    [Authorize(Roles = $"{RoleNames.Ceo},{RoleNames.Admin},{RoleNames.SuperAdmin}")]
    public async Task<ActionResult<IReadOnlyCollection<TimeCountChartItemResponse>>> GetCeoTripVolume(
        CancellationToken cancellationToken)
    {
        return Ok(await GetMonthlyTripVolumeAsync(6, cancellationToken));
    }

    [HttpGet("inventory-stock-levels")]
    [Authorize(Roles = $"{RoleNames.InventoryOfficer},{RoleNames.Manager},{RoleNames.Admin},{RoleNames.SuperAdmin}")]
    public async Task<ActionResult<IReadOnlyCollection<InventoryStockLevelChartItemResponse>>> GetInventoryStockLevels(
        CancellationToken cancellationToken)
    {
        var rows = await _dbContext.InventoryItems
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.Quantity <= (item.ReorderLevel ?? 0m) ? 0 : 1)
            .ThenBy(item => item.Quantity)
            .Take(15)
            .Select(item => new InventoryStockLevelChartItemResponse(
                item.Name,
                item.Quantity,
                item.ReorderLevel,
                item.ReorderLevel.HasValue && item.Quantity <= item.ReorderLevel.Value))
            .ToListAsync(cancellationToken);

        return Ok(rows);
    }

    [HttpGet("inventory-weekly-requests")]
    [Authorize(Roles = $"{RoleNames.InventoryOfficer},{RoleNames.Manager},{RoleNames.Admin},{RoleNames.SuperAdmin}")]
    public async Task<ActionResult<IReadOnlyCollection<TimeCountChartItemResponse>>> GetInventoryWeeklyRequests(
        CancellationToken cancellationToken)
    {
        var weekStarts = GetRecentWeekStarts(8);
        var from = weekStarts[0];
        var to = weekStarts[^1].AddDays(7);
        var requests = await _dbContext.Requests
            .AsNoTracking()
            .Where(request => request.CreatedAt >= from && request.CreatedAt < to)
            .Select(request => request.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(weekStarts
            .Select(weekStart => new TimeCountChartItemResponse(
                FormatWeekLabel(weekStart),
                requests.Count(createdAt => createdAt >= weekStart && createdAt < weekStart.AddDays(7))))
            .ToList());
    }

    private async Task<Guid?> GetCurrentCustomerIdAsync(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        return await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.CustomerId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<int> CountMissingRequiredDocumentsAsync(
        IReadOnlyCollection<Guid> tripIds,
        CancellationToken cancellationToken)
    {
        if (tripIds.Count == 0)
        {
            return 0;
        }

        var requiredTypes = DispatchDocumentRules.GetRequiredDocumentTypes(_dispatchingOptions).ToArray();
        if (requiredTypes.Length == 0)
        {
            return 0;
        }

        var documents = await _dbContext.DispatchTripDocuments
            .AsNoTracking()
            .Where(document => tripIds.Contains(document.TripId) && document.IsActive && requiredTypes.Contains(document.Type))
            .Select(document => new TripDocumentProjection(document.TripId, document.Type, document.State))
            .ToListAsync(cancellationToken);

        var documentsByTrip = documents
            .GroupBy(document => document.TripId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var missing = 0;
        foreach (var tripId in tripIds)
        {
            documentsByTrip.TryGetValue(tripId, out var tripDocuments);
            foreach (var requiredType in requiredTypes)
            {
                var hasUploadedOrVerified = tripDocuments?.Any(document =>
                    document.Type == requiredType &&
                    document.State is TripDocumentState.Uploaded or TripDocumentState.Verified) == true;

                if (!hasUploadedOrVerified)
                {
                    missing++;
                }
            }
        }

        return missing;
    }

    private async Task<HashSet<Guid>> GetDocumentCompliantTripIdsAsync(
        IReadOnlyCollection<Guid> tripIds,
        CancellationToken cancellationToken)
    {
        if (tripIds.Count == 0)
        {
            return [];
        }

        var requiredTypes = DispatchDocumentRules.GetRequiredDocumentTypes(_dispatchingOptions).ToArray();
        if (requiredTypes.Length == 0)
        {
            return tripIds.ToHashSet();
        }

        var documents = await _dbContext.DispatchTripDocuments
            .AsNoTracking()
            .Where(document => tripIds.Contains(document.TripId) && document.IsActive && requiredTypes.Contains(document.Type))
            .Select(document => new TripDocumentProjection(document.TripId, document.Type, document.State))
            .ToListAsync(cancellationToken);

        var documentsByTrip = documents
            .GroupBy(document => document.TripId)
            .ToDictionary(group => group.Key, group => group.ToList());

        return tripIds
            .Where(tripId =>
            {
                documentsByTrip.TryGetValue(tripId, out var tripDocuments);
                return requiredTypes.All(requiredType =>
                    tripDocuments?.Any(document =>
                        document.Type == requiredType &&
                        document.State is TripDocumentState.Uploaded or TripDocumentState.Verified) == true);
            })
            .ToHashSet();
    }

    private async Task<decimal> CalculateDocumentComplianceRateAsync(CancellationToken cancellationToken)
    {
        var deliveredTripIds = await _dbContext.DispatchTripStatusHistories
            .AsNoTracking()
            .Where(history => history.ToStatus == TripStatus.Delivered)
            .Select(history => history.TripId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var currentlyCompletedTripIds = await _dbContext.DispatchTrips
            .AsNoTracking()
            .Where(trip => trip.Status == TripStatus.Delivered || trip.Status == TripStatus.Closed)
            .Select(trip => trip.Id)
            .ToListAsync(cancellationToken);

        deliveredTripIds = deliveredTripIds
            .Concat(currentlyCompletedTripIds)
            .Distinct()
            .ToList();

        if (deliveredTripIds.Count == 0)
        {
            return 0m;
        }

        var requiredTypes = DispatchDocumentRules.GetRequiredDocumentTypes(_dispatchingOptions).ToArray();
        if (requiredTypes.Length == 0)
        {
            return 100m;
        }

        var documents = await _dbContext.DispatchTripDocuments
            .AsNoTracking()
            .Where(document => deliveredTripIds.Contains(document.TripId) && document.IsActive && requiredTypes.Contains(document.Type))
            .Select(document => new TripDocumentProjection(document.TripId, document.Type, document.State))
            .ToListAsync(cancellationToken);

        var documentsByTrip = documents
            .GroupBy(document => document.TripId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var compliantTrips = deliveredTripIds.Count(tripId =>
        {
            documentsByTrip.TryGetValue(tripId, out var tripDocuments);
            return requiredTypes.All(requiredType =>
                tripDocuments?.Any(document =>
                    document.Type == requiredType &&
                    document.State == TripDocumentState.Verified) == true);
        });

        return CalculatePercent(compliantTrips, deliveredTripIds.Count);
    }

    private async Task<List<Guid>> GetDeliveredThisMonthTripIdsAsync(
        DateTime monthStart,
        DateTime nextMonthStart,
        CancellationToken cancellationToken)
    {
        var deliveredByHistory = await _dbContext.DispatchTripStatusHistories
            .AsNoTracking()
            .Where(history => history.ToStatus == TripStatus.Delivered)
            .Where(history => history.EventAt >= monthStart && history.EventAt < nextMonthStart)
            .Select(history => history.TripId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var deliveredByCurrentStatus = await _dbContext.DispatchTrips
            .AsNoTracking()
            .Where(trip => trip.CreatedAt >= monthStart && trip.CreatedAt < nextMonthStart)
            .Where(trip => trip.Status == TripStatus.Delivered || trip.Status == TripStatus.Closed)
            .Select(trip => trip.Id)
            .ToListAsync(cancellationToken);

        return deliveredByHistory
            .Concat(deliveredByCurrentStatus)
            .Distinct()
            .ToList();
    }

    private async Task<IReadOnlyCollection<TimeCountChartItemResponse>> GetMonthlyTripVolumeAsync(
        int monthCount,
        CancellationToken cancellationToken)
    {
        var monthStarts = GetRecentMonthStarts(monthCount);
        var from = monthStarts[0];
        var to = monthStarts[^1].AddMonths(1);
        var trips = await _dbContext.DispatchTrips
            .AsNoTracking()
            .Where(trip => trip.CreatedAt >= from && trip.CreatedAt < to)
            .Select(trip => trip.CreatedAt)
            .ToListAsync(cancellationToken);

        return monthStarts
            .Select(monthStart => new TimeCountChartItemResponse(
                FormatMonthLabel(monthStart),
                trips.Count(createdAt => createdAt >= monthStart && createdAt < monthStart.AddMonths(1))))
            .ToList();
    }

    private async Task<IReadOnlyCollection<RoleCountChartItemResponse>> GetActiveUsersByRoleAsync(
        CancellationToken cancellationToken)
    {
        var roleCounts = await _dbContext.UserRoles
            .AsNoTracking()
            .Join(
                _dbContext.Users.AsNoTracking().Where(user => user.IsActive),
                userRole => userRole.UserId,
                user => user.Id,
                (userRole, user) => userRole)
            .GroupBy(userRole => userRole.RoleId)
            .Select(group => new { RoleId = group.Key, Count = group.Count() })
            .OrderByDescending(item => item.Count)
            .ToListAsync(cancellationToken);

        var roleIds = roleCounts.Select(item => item.RoleId).ToList();
        var roleNames = await _dbContext.Roles
            .AsNoTracking()
            .Where(role => roleIds.Contains(role.Id))
            .Select(role => new { role.Id, role.Name })
            .ToDictionaryAsync(role => role.Id, role => role.Name, cancellationToken);

        return roleCounts
            .Select(item => new RoleCountChartItemResponse(
                roleNames.GetValueOrDefault(item.RoleId, "Unknown"),
                item.Count))
            .ToList();
    }

    private async Task<IReadOnlyCollection<FinancialBreakdownChartItemResponse>> GetFinancialBreakdownAsync(
        DateTime from,
        DateTime to,
        IReadOnlyCollection<DateTime> periodStarts,
        CancellationToken cancellationToken)
    {
        var trips = await _dbContext.DispatchTrips
            .AsNoTracking()
            .Where(trip => trip.CreatedAt >= from && trip.CreatedAt < to)
            .Where(trip => ActiveOrDeliveredStatuses.Contains(trip.Status))
            .Select(trip => new FinancialChartProjection(
                trip.CreatedAt,
                trip.Rate,
                trip.Payroll,
                trip.FuelAmount,
                trip.FuelPricePerLiter))
            .ToListAsync(cancellationToken);

        return periodStarts
            .Select(periodStart =>
            {
                var periodEnd = periodStarts.Count == 6
                    ? periodStart.AddMonths(1)
                    : periodStart.AddDays(7);
                var periodTrips = trips
                    .Where(trip => trip.CreatedAt >= periodStart && trip.CreatedAt < periodEnd)
                    .ToList();

                return new FinancialBreakdownChartItemResponse(
                    periodStarts.Count == 6 ? FormatMonthLabel(periodStart) : FormatWeekLabel(periodStart),
                    periodTrips.Sum(trip => trip.Rate ?? 0m),
                    periodTrips.Sum(trip => trip.Payroll ?? 0m),
                    periodTrips.Sum(CalculateFuelCost));
            })
            .ToList();
    }

    private static (DateTime MonthStart, DateTime NextMonthStart) GetCurrentUtcMonthRange()
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return (monthStart, monthStart.AddMonths(1));
    }

    private static List<DateTime> GetRecentWeekStarts(int weekCount)
    {
        var currentWeekStart = GetUtcWeekStart(DateTime.UtcNow.Date);
        return Enumerable.Range(0, weekCount)
            .Select(offset => currentWeekStart.AddDays(-7 * (weekCount - 1 - offset)))
            .ToList();
    }

    private static List<DateTime> GetRecentMonthStarts(int monthCount)
    {
        var now = DateTime.UtcNow;
        var currentMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return Enumerable.Range(0, monthCount)
            .Select(offset => currentMonthStart.AddMonths(-(monthCount - 1 - offset)))
            .ToList();
    }

    private static DateTime GetUtcWeekStart(DateTime today)
    {
        var offset = today.DayOfWeek == DayOfWeek.Sunday
            ? 6
            : (int)today.DayOfWeek - (int)DayOfWeek.Monday;
        return today.AddDays(-offset);
    }

    private static decimal CalculatePercent(int numerator, int denominator)
    {
        return denominator == 0
            ? 0m
            : Math.Round((decimal)numerator / denominator * 100m, 1);
    }

    private static decimal CalculateFuelCost(FinancialChartProjection trip)
    {
        if (!trip.FuelAmount.HasValue)
        {
            return 0m;
        }

        return trip.FuelPricePerLiter.HasValue
            ? trip.FuelAmount.Value * trip.FuelPricePerLiter.Value
            : trip.FuelAmount.Value;
    }

    private static bool IsDeliveredOnTime(TripDeliveryTimingProjection trip)
    {
        var completedAt = trip.UpdatedAt ?? trip.CreatedAt;
        return completedAt <= trip.CreatedAt.AddHours(24);
    }

    private static bool IsDeliveredOnTime(DriverRecentTripProjection trip)
    {
        var completedAt = trip.UpdatedAt ?? trip.CreatedAt;
        return completedAt <= trip.CreatedAt.AddHours(24);
    }

    private static IReadOnlyCollection<DocumentAlertChartItemResponse> WithDocumentTypes(
        IReadOnlyCollection<DocumentAlertChartItemResponse> rows)
    {
        var byType = rows.ToDictionary(row => row.DocumentType, row => row.PendingCount, StringComparer.OrdinalIgnoreCase);
        return Enum.GetValues<TripDocumentType>()
            .Select(type => new DocumentAlertChartItemResponse(type.ToString(), byType.GetValueOrDefault(type.ToString())))
            .ToList();
    }

    private static string FormatWeekLabel(DateTime weekStart)
    {
        return weekStart.ToString("MMM d", CultureInfo.InvariantCulture);
    }

    private static string FormatMonthLabel(DateTime monthStart)
    {
        return monthStart.ToString("MMM yyyy", CultureInfo.InvariantCulture);
    }

    private async Task RecordFinancialAccessAsync(
        string endpoint,
        string[] fields,
        CancellationToken cancellationToken)
    {
        _auditService.AddEntry(
            User.GetUserId(),
            AuditActions.FinancialFieldAccessed,
            EntityTypes.DispatchTrip,
            Guid.Empty,
            null,
            new
            {
                Endpoint = endpoint,
                Fields = fields
            });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private sealed record TripFinancialProjection(decimal? Rate, decimal? Payroll);

    private sealed record TripDocumentProjection(Guid TripId, TripDocumentType Type, TripDocumentState State);

    private sealed record TripStatusCreatedProjection(TripStatus Status, DateTime CreatedAt);

    private sealed record TripDeliveryTimingProjection(DateTime CreatedAt, DateTime? UpdatedAt);

    private sealed record DriverRecentTripProjection(Guid Id, DateTime CreatedAt, DateTime? UpdatedAt);

    private sealed record TripTruckCreatedProjection(DateTime CreatedAt, Guid TruckAssetId);

    private sealed record FinancialChartProjection(
        DateTime CreatedAt,
        decimal? Rate,
        decimal? Payroll,
        decimal? FuelAmount,
        decimal? FuelPricePerLiter);
}

public sealed record DispatchDashboardKpisResponse(
    int ActiveTrips,
    int DriversOnRoad,
    int DriversAvailable,
    int DocumentAlerts,
    int PendingShipmentRequests,
    int TripsOnHold,
    int TripsInDraft,
    int TripsFailedAttempt,
    int ApprovedShipmentRequests,
    int IncompleteDocumentAlerts,
    IReadOnlyCollection<StatusCountChartItemResponse> StatusBreakdown);

public sealed record FinanceDashboardKpisResponse(
    decimal TotalTripValueThisMonth,
    decimal TotalPayrollThisMonth,
    int PendingFinancePoCount,
    int TripsWithMissingRate);

public sealed record DriverDashboardKpisResponse(
    DriverActiveTripKpiResponse? MyActiveTrip,
    int MyTripsToday,
    int MyTripsThisWeek,
    int MyPendingDocuments,
    IReadOnlyCollection<TimeCountChartItemResponse> DailyTrips,
    decimal OnTimeRate,
    decimal DocComplianceRate);

public sealed record DriverActiveTripKpiResponse(
    Guid Id,
    TripStatus Status,
    string? CustomerName,
    string? TruckAssetCode);

public sealed record CustomerDashboardKpisResponse(
    int MyActiveRequests,
    IReadOnlyCollection<CustomerActiveRequestStatusKpiResponse> MyActiveRequestStatuses,
    int MyDeliveredThisMonth,
    int MyPendingDocuments,
    IReadOnlyCollection<StatusCountChartItemResponse> StatusBreakdown);

public sealed record CustomerActiveRequestStatusKpiResponse(
    ShipmentRequestStatus Status,
    int Count);

public sealed record SystemDashboardKpisResponse(
    int TotalUsers,
    int TotalTripsThisMonth,
    decimal FleetUtilizationPercent,
    decimal DocumentComplianceRate,
    IReadOnlyCollection<TimeCountChartItemResponse> MonthlyVolume,
    IReadOnlyCollection<RoleCountChartItemResponse> UsersByRole);

public sealed record StatusCountChartItemResponse(
    string Status,
    int Count);

public sealed record TimeCountChartItemResponse(
    string Period,
    int Count);

public sealed record DocumentAlertChartItemResponse(
    string DocumentType,
    int PendingCount);

public sealed record PercentByPeriodChartItemResponse(
    string Period,
    decimal Percent);

public sealed record DriverUtilizationChartItemResponse(
    string DriverName,
    int Trips);

public sealed record OnTimeDelayedChartItemResponse(
    string Period,
    int OnTime,
    int Delayed);

public sealed record FinancialRevenueChartItemResponse(
    string Period,
    decimal Revenue);

public sealed record FinancialBreakdownChartItemResponse(
    string Period,
    decimal Revenue,
    decimal Payroll,
    decimal Fuel);

public sealed record RoleCountChartItemResponse(
    string Role,
    int Count);

public sealed record InventoryStockLevelChartItemResponse(
    string ItemName,
    decimal Quantity,
    decimal? ReorderLevel,
    bool IsLowStock);
