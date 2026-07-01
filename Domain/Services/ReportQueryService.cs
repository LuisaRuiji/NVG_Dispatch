
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Entities;
using NVGInventory.Domain.Enums;
using NVGInventory.Modules.Dispatching;
using NVGInventory.Modules.Dispatching.Entities;
using NVGInventory.Modules.Dispatching.Enums;

namespace NVGInventory.Domain.Services;

public sealed record StockMovementReportItem(
    Guid InventoryId,
    string InventoryName,
    decimal TotalIn,
    decimal TotalOut,
    decimal TotalBorrow,
    decimal TotalReturn,
    decimal TotalAdjustment);

public sealed record LowStockReportItem(
    Guid InventoryId,
    string InventoryName,
    decimal Quantity,
    decimal ReorderLevel);

public sealed record OpenLoanReportItem(
    Guid LoanId,
    string BorrowerUsername,
    string? AssetCode,
    decimal TotalItemsBorrowed,
    decimal TotalItemsReturned,
    LoanStatus Status,
    DateTime IssuedAt);

public sealed record InventoryValuationReportItem(
    Guid InventoryId,
    string InventoryName,
    decimal Quantity,
    decimal AverageCost,
    decimal TotalValue);

public sealed record AssetMaintenanceCostReportItem(
    Guid AssetId,
    string AssetCode,
    decimal TotalMaintenanceCost,
    decimal TotalItemsConsumed,
    int RequestCount);

public sealed record AssetConsumptionBreakdownItem(
    Guid InventoryId,
    string InventoryName,
    decimal TotalQuantityUsed,
    decimal TotalCost);

public sealed record AssetConsumptionItem(
    Guid InventoryId,
    string InventoryName,
    decimal TotalItemsConsumed,
    decimal TotalCost);

public sealed record AssetConsumptionSummary(
    Guid AssetId,
    string AssetCode,
    IReadOnlyCollection<AssetConsumptionItem> Items);

public sealed record AdjustmentReportItem(
    Guid RequestId,
    string? Reason,
    Guid InventoryId,
    string InventoryName,
    decimal Quantity,
    Guid ActorUserId,
    string ActorUsername,
    DateTime CreatedAt);

public sealed record SupplierSpendReportItem(
    Guid SupplierId,
    string SupplierName,
    decimal TotalSpend,
    int TotalPurchaseOrders,
    int TotalLines,
    decimal TotalQty,
    decimal AveragePurchaseOrderValue);

public sealed record DispatchTripStatusCountReportItem(
    TripStatus Status,
    int Count);

public sealed record DispatchTripWeeklyCountReportItem(
    DateTime WeekStart,
    int Count);

public sealed record DispatchTripSummaryReport(
    IReadOnlyCollection<DispatchTripStatusCountReportItem> StatusCounts,
    IReadOnlyCollection<DispatchTripWeeklyCountReportItem> WeeklyCounts,
    int DeliveredTrips,
    int TotalNonCancelledTrips,
    decimal CompletionRatePercent);

public sealed record DispatchDriverPerformanceReportItem(
    Guid DriverUserId,
    string DriverName,
    int TripsCompleted,
    decimal? AverageDeliveryMinutes,
    decimal OnTimeRatePercent,
    decimal DocumentComplianceRatePercent);

public sealed record DispatchDeliveryTimeRouteReportItem(
    string RouteKey,
    string FromLocation,
    string ToLocation,
    int TripCount,
    decimal AverageDeliveryMinutes,
    decimal LongestDeliveryMinutes,
    DateTime GeneratedAt);

public sealed record DispatchDeliveryTimeReport(
    PagedQueryResult<DispatchDeliveryTimeRouteReportItem> Routes,
    IReadOnlyCollection<DispatchDeliveryTimeRouteReportItem> LongestRoutes,
    DateTime GeneratedAt);

public sealed record DispatchDocumentProcessingReportItem(
    string DocumentType,
    int PendingVerification,
    decimal? AverageVerificationHours,
    decimal RejectionRatePercent);

public sealed record DispatchFinancialPeriodReportItem(
    DateTime PeriodStart,
    string PeriodLabel,
    decimal TotalTripRevenue,
    decimal TotalDriverPayroll,
    decimal TotalFuelCost);

public sealed record DispatchDriverPayrollReportItem(
    Guid? DriverUserId,
    string DriverName,
    int DeliveredTrips,
    decimal TotalPayroll);

public sealed record DispatchFinancialSummaryReport(
    IReadOnlyCollection<DispatchFinancialPeriodReportItem> Periods,
    IReadOnlyCollection<DispatchDriverPayrollReportItem> DriverPayroll);

public sealed record AuditLogItem(
    Guid Id,
    string Action,
    string EntityType,
    Guid EntityId,
    Guid ActorUserId,
    string? ActorUsername,
    string? ActorRole,
    DateTime CreatedAt,
    string? Metadata);

public sealed record AuthEventItem(
    Guid Id,
    string EventType,
    string Outcome,
    string? ReasonCode,
    string? Username,
    Guid? UserId,
    string? RolesSnapshotJson,
    string AuthMethod,
    bool MfaPerformed,
    string? MfaMethod,
    string? TokenJti,
    string? CorrelationId,
    string? IpAddress,
    string? UserAgent,
    string? ClientApp,
    string? Environment,
    DateTime CreatedAt);

public sealed record IntegrityCheckResult(
    int MaintenanceWithoutAsset,
    int LoansWithNegativeRemaining,
    int PurchaseOrdersOverReceived,
    int InventoryNegativeQuantity,
    int RequestsIssuedWithoutStockLogs,
    int DuplicateIssueLogs,
    int OrphanStockLogs,
    int OrphanApprovalActions,
    int PoWithoutWorkflow,
    int AdjustmentWithoutLogs,
    int SupplierActionsWithoutAudit,
    int PoReceiptsWithoutAudit,
    int LoanReturnsWithoutAudit,
    int AdjustmentsWithoutAudit,
    int InactiveSuppliersReferenced,
    int InactiveInventoryReferenced);

public sealed class ReportQueryService
{
    private readonly InventoryDbContext _dbContext;
    private readonly TripDocumentType[] _requiredDispatchDocumentTypes;

    private sealed class SupplierSpendAggregateProjection
    {
        public Guid SupplierId { get; init; }
        public string SupplierName { get; init; } = string.Empty;
        public decimal TotalSpend { get; init; }
        public decimal TotalQty { get; init; }
        public int TotalLines { get; init; }
        public int TotalPurchaseOrders { get; init; }
    }

    public ReportQueryService(InventoryDbContext dbContext, IOptions<DispatchingOptions>? dispatchingOptions = null)
    {
        _dbContext = dbContext;
        _requiredDispatchDocumentTypes = DispatchDocumentRules
            .GetRequiredDocumentTypes(dispatchingOptions?.Value ?? new DispatchingOptions())
            .ToArray();
    }

    public async Task<IReadOnlyCollection<StockMovementReportItem>> GetStockMovementReportAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        Guid? inventoryId,
        CancellationToken cancellationToken = default)
    {
        var logsQuery = _dbContext.StockLogs.AsNoTracking().AsQueryable();

        if (fromUtc.HasValue)
        {
            logsQuery = logsQuery.Where(log => log.CreatedAt >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            logsQuery = logsQuery.Where(log => log.CreatedAt <= toUtc.Value);
        }

        if (inventoryId.HasValue)
        {
            logsQuery = logsQuery.Where(log => log.InventoryId == inventoryId.Value);
        }

        var aggregates = await logsQuery
            .GroupBy(log => log.InventoryId)
            .Select(group => new
            {
                InventoryId = group.Key,
                TotalIn = group
                    .Where(log => log.MovementType == StockMovementType.In)
                    .Sum(log => Math.Abs(log.QtyDelta)),
                TotalOut = group
                    .Where(log => log.MovementType == StockMovementType.Out)
                    .Sum(log => Math.Abs(log.QtyDelta)),
                TotalBorrow = group
                    .Where(log => log.MovementType == StockMovementType.Borrow)
                    .Sum(log => Math.Abs(log.QtyDelta)),
                TotalReturn = group
                    .Where(log => log.MovementType == StockMovementType.Return)
                    .Sum(log => log.QtyDelta),
                TotalAdjustment = group
                    .Where(log => log.MovementType == StockMovementType.Adjustment)
                    .Sum(log => log.QtyDelta)
            })
            .ToListAsync(cancellationToken);

        var inventoryQuery = _dbContext.InventoryItems.AsNoTracking().AsQueryable();
        if (inventoryId.HasValue)
        {
            inventoryQuery = inventoryQuery.Where(item => item.Id == inventoryId.Value);
        }

        var inventoryItems = await inventoryQuery
            .Select(item => new { item.Id, item.Name })
            .ToListAsync(cancellationToken);

        var aggregateById = aggregates.ToDictionary(item => item.InventoryId, item => item);

        return inventoryItems
            .Select(item =>
            {
                aggregateById.TryGetValue(item.Id, out var aggregate);
                return new StockMovementReportItem(
                    item.Id,
                    item.Name,
                    aggregate?.TotalIn ?? 0m,
                    aggregate?.TotalOut ?? 0m,
                    aggregate?.TotalBorrow ?? 0m,
                    aggregate?.TotalReturn ?? 0m,
                    aggregate?.TotalAdjustment ?? 0m);
            })
            .OrderBy(item => item.InventoryName)
            .ToList();
    }

    public async Task<IReadOnlyCollection<LowStockReportItem>> GetLowStockReportAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.InventoryItems
            .AsNoTracking()
            .Where(item => item.IsActive && item.ReorderLevel.HasValue && item.Quantity <= item.ReorderLevel.Value)
            .OrderBy(item => item.Name)
            .Select(item => new LowStockReportItem(
                item.Id,
                item.Name,
                item.Quantity,
                item.ReorderLevel ?? 0m))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<OpenLoanReportItem>> GetOpenLoanReportAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Loans
            .AsNoTracking()
            .Include(loan => loan.Borrower)
            .Include(loan => loan.Asset)
            .Include(loan => loan.Lines)
            .Where(loan => loan.Status == LoanStatus.Open || loan.Status == LoanStatus.PartiallyReturned)
            .OrderByDescending(loan => loan.IssuedAt)
            .Select(loan => new OpenLoanReportItem(
                loan.Id,
                loan.Borrower != null ? loan.Borrower.Username : string.Empty,
                loan.Asset != null ? loan.Asset.AssetCode : null,
                loan.Lines.Sum(line => line.QtyIssued),
                loan.Lines.Sum(line => line.QtyReturned),
                loan.Status,
                loan.IssuedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<InventoryValuationReportItem>> GetInventoryValuationReportAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.InventoryItems
            .AsNoTracking()
            .OrderBy(item => item.Name)
            .Select(item => new InventoryValuationReportItem(
                item.Id,
                item.Name,
                item.Quantity,
                item.AverageCost,
                item.Quantity * item.AverageCost))
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedQueryResult<AssetMaintenanceCostReportItem>> GetAssetMaintenanceCostReportAsync(
        Guid? assetId,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var requestsQuery = _dbContext.Requests
            .AsNoTracking()
            .Where(request =>
                request.RequestType == RequestType.MaintenanceIssue
                && request.AssetId.HasValue);

        if (assetId.HasValue)
        {
            requestsQuery = requestsQuery.Where(request => request.AssetId == assetId.Value);
        }

        var logsQuery = _dbContext.StockLogs
            .AsNoTracking()
            .Where(log =>
                log.RefType == EntityTypes.Request
                && log.MovementType == StockMovementType.Out);

        if (fromUtc.HasValue)
        {
            logsQuery = logsQuery.Where(log => log.CreatedAt >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            logsQuery = logsQuery.Where(log => log.CreatedAt <= toUtc.Value);
        }

        var costsQuery =
            from log in logsQuery
            join request in requestsQuery on log.RefId equals request.Id
            join asset in _dbContext.Assets.AsNoTracking() on request.AssetId equals asset.Id
            select new
            {
                AssetId = asset.Id,
                asset.AssetCode,
                RequestId = request.Id,
                QtyConsumed = Math.Abs(log.QtyDelta),
                Cost = log.TotalCostSnapshot ?? 0m
            };

        var groupedQuery = costsQuery
            .GroupBy(entry => new { entry.AssetId, entry.AssetCode })
            .Select(group => new
            {
                group.Key.AssetId,
                group.Key.AssetCode,
                TotalMaintenanceCost = group.Sum(entry => entry.Cost),
                TotalItemsConsumed = group.Sum(entry => entry.QtyConsumed),
                RequestCount = group.Select(entry => entry.RequestId).Distinct().Count()
            });

        var totalCount = await groupedQuery.CountAsync(cancellationToken);
        var items = await groupedQuery
            .OrderByDescending(item => item.TotalMaintenanceCost)
            .ThenBy(item => item.AssetCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new AssetMaintenanceCostReportItem(
                item.AssetId,
                item.AssetCode,
                item.TotalMaintenanceCost,
                item.TotalItemsConsumed,
                item.RequestCount))
            .ToListAsync(cancellationToken);

        return new PagedQueryResult<AssetMaintenanceCostReportItem>(items, totalCount);
    }

    public async Task<IReadOnlyCollection<AssetConsumptionBreakdownItem>> GetAssetConsumptionBreakdownAsync(
        Guid assetId,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken = default)
    {
        var requestsQuery = _dbContext.Requests
            .AsNoTracking()
            .Where(request =>
                request.RequestType == RequestType.MaintenanceIssue
                && request.AssetId == assetId);

        var logsQuery = _dbContext.StockLogs
            .AsNoTracking()
            .Where(log =>
                log.RefType == EntityTypes.Request
                && log.MovementType == StockMovementType.Out);

        if (fromUtc.HasValue)
        {
            logsQuery = logsQuery.Where(log => log.CreatedAt >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            logsQuery = logsQuery.Where(log => log.CreatedAt <= toUtc.Value);
        }

        var breakdownQuery =
            from log in logsQuery
            join request in requestsQuery on log.RefId equals request.Id
            join item in _dbContext.InventoryItems.AsNoTracking() on log.InventoryId equals item.Id
            select new
            {
                item.Id,
                item.Name,
                QtyConsumed = Math.Abs(log.QtyDelta),
                Cost = log.TotalCostSnapshot ?? 0m
            };

        return await breakdownQuery
            .GroupBy(entry => new { entry.Id, entry.Name })
            .Select(group => new
            {
                group.Key.Id,
                group.Key.Name,
                TotalQuantityUsed = group.Sum(entry => entry.QtyConsumed),
                TotalCost = group.Sum(entry => entry.Cost)
            })
            .OrderByDescending(item => item.TotalCost)
            .ThenBy(item => item.Name)
            .Select(item => new AssetConsumptionBreakdownItem(
                item.Id,
                item.Name,
                item.TotalQuantityUsed,
                item.TotalCost))
            .ToListAsync(cancellationToken);
    }

    public Task<int> GetMaintenanceIssuesWithoutAssetCountAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.Requests
            .AsNoTracking()
            .Where(request => request.RequestType == RequestType.MaintenanceIssue && !request.AssetId.HasValue)
            .CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<AdjustmentReportItem>> GetAdjustmentReportAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken = default)
    {
        var logsQuery = _dbContext.StockLogs
            .AsNoTracking()
            .Where(log => log.RefType == EntityTypes.Request && log.MovementType == StockMovementType.Adjustment);

        if (fromUtc.HasValue)
        {
            logsQuery = logsQuery.Where(log => log.CreatedAt >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            logsQuery = logsQuery.Where(log => log.CreatedAt <= toUtc.Value);
        }

        var query =
            from log in logsQuery
            join request in _dbContext.Requests.AsNoTracking() on log.RefId equals request.Id
            join item in _dbContext.InventoryItems.AsNoTracking() on log.InventoryId equals item.Id
            join user in _dbContext.Users.AsNoTracking() on log.ActorUserId equals user.Id
            where request.RequestType == RequestType.AdjustmentDamageLoss
            orderby log.CreatedAt descending
            select new AdjustmentReportItem(
                request.Id,
                request.Purpose,
                item.Id,
                item.Name,
                Math.Abs(log.QtyDelta),
                user.Id,
                user.Username,
                log.CreatedAt);

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<PagedQueryResult<SupplierSpendReportItem>> GetSupplierSpendReportAsync(
        SupplierSpendBasis basis,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        IQueryable<SupplierSpendAggregateProjection> supplierTotalsQuery;

        if (basis == SupplierSpendBasis.Received)
        {
            var receiptsQuery = _dbContext.PurchaseOrderReceipts
                .AsNoTracking()
                .AsQueryable();

            if (fromUtc.HasValue)
            {
                receiptsQuery = receiptsQuery.Where(receipt => receipt.ReceivedAt >= fromUtc.Value);
            }

            if (toUtc.HasValue)
            {
                receiptsQuery = receiptsQuery.Where(receipt => receipt.ReceivedAt <= toUtc.Value);
            }

            var receivedQuery =
                from receipt in receiptsQuery
                join line in _dbContext.PurchaseOrderLines.AsNoTracking() on receipt.PurchaseOrderLineId equals line.Id
                join order in _dbContext.PurchaseOrders.AsNoTracking() on line.PurchaseOrderId equals order.Id
                join supplier in _dbContext.Suppliers.AsNoTracking() on order.SupplierId equals supplier.Id
                where order.Status == PurchaseOrderStatus.PartiallyReceived
                      || order.Status == PurchaseOrderStatus.Closed
                      || order.Status == PurchaseOrderStatus.Approved
                select new
                {
                    SupplierId = supplier.Id,
                    SupplierName = supplier.Name,
                    PurchaseOrderId = order.Id,
                    PurchaseOrderLineId = line.Id,
                    QtyReceived = receipt.QtyReceivedIncrement,
                    UnitPrice = line.UnitPrice ?? 0m
                };

            supplierTotalsQuery = receivedQuery
                .GroupBy(entry => new { entry.SupplierId, entry.SupplierName })
                .Select(group => new SupplierSpendAggregateProjection
                {
                    SupplierId = group.Key.SupplierId,
                    SupplierName = group.Key.SupplierName,
                    TotalSpend = group.Sum(entry => entry.QtyReceived * entry.UnitPrice),
                    TotalQty = group.Sum(entry => entry.QtyReceived),
                    TotalLines = group.Select(entry => entry.PurchaseOrderLineId).Distinct().Count(),
                    TotalPurchaseOrders = group.Select(entry => entry.PurchaseOrderId).Distinct().Count()
                });
        }
        else
        {
            var ordersQuery = _dbContext.PurchaseOrders
                .AsNoTracking()
                .Where(order => order.Status != PurchaseOrderStatus.Draft && order.Status != PurchaseOrderStatus.Rejected)
                .AsQueryable();

            if (fromUtc.HasValue)
            {
                ordersQuery = ordersQuery.Where(order => order.CreatedAt >= fromUtc.Value);
            }

            if (toUtc.HasValue)
            {
                ordersQuery = ordersQuery.Where(order => order.CreatedAt <= toUtc.Value);
            }

            var orderedQuery =
                from order in ordersQuery
                join line in _dbContext.PurchaseOrderLines.AsNoTracking() on order.Id equals line.PurchaseOrderId
                join supplier in _dbContext.Suppliers.AsNoTracking() on order.SupplierId equals supplier.Id
                select new
                {
                    SupplierId = supplier.Id,
                    SupplierName = supplier.Name,
                    PurchaseOrderId = order.Id,
                    PurchaseOrderLineId = line.Id,
                    QtyOrdered = line.QtyOrdered,
                    UnitPrice = line.UnitPrice ?? 0m
                };

            supplierTotalsQuery = orderedQuery
                .GroupBy(entry => new { entry.SupplierId, entry.SupplierName })
                .Select(group => new SupplierSpendAggregateProjection
                {
                    SupplierId = group.Key.SupplierId,
                    SupplierName = group.Key.SupplierName,
                    TotalSpend = group.Sum(entry => entry.QtyOrdered * entry.UnitPrice),
                    TotalQty = group.Sum(entry => entry.QtyOrdered),
                    TotalLines = group.Count(),
                    TotalPurchaseOrders = group.Select(entry => entry.PurchaseOrderId).Distinct().Count()
                });
        }

        var totalCount = await supplierTotalsQuery.CountAsync(cancellationToken);

        var items = await supplierTotalsQuery
            .OrderByDescending(item => item.TotalSpend)
            .ThenBy(item => item.SupplierName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var responseItems = items
            .Select(item => new SupplierSpendReportItem(
                item.SupplierId,
                item.SupplierName,
                item.TotalSpend,
                item.TotalPurchaseOrders,
                item.TotalLines,
                item.TotalQty,
                item.TotalPurchaseOrders == 0
                    ? 0m
                    : item.TotalSpend / item.TotalPurchaseOrders))
            .ToList();

        return new PagedQueryResult<SupplierSpendReportItem>(responseItems, totalCount);
    }

    public async Task<DispatchTripSummaryReport> GetDispatchTripSummaryReportAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken = default)
    {
        var tripsQuery = ApplyTripCreatedRange(_dbContext.DispatchTrips.AsNoTracking(), fromUtc, toUtc);

        var statusCounts = await tripsQuery
            .GroupBy(trip => trip.Status)
            .Select(group => new DispatchTripStatusCountReportItem(group.Key, group.Count()))
            .ToListAsync(cancellationToken);

        var createdDates = await tripsQuery
            .Select(trip => trip.CreatedAt)
            .ToListAsync(cancellationToken);

        var weeklyCounts = createdDates
            .GroupBy(GetWeekStart)
            .Select(group => new DispatchTripWeeklyCountReportItem(group.Key, group.Count()))
            .OrderBy(item => item.WeekStart)
            .ToList();

        var totalNonCancelledTrips = statusCounts
            .Where(item => item.Status != TripStatus.Cancelled)
            .Sum(item => item.Count);
        var deliveredTrips = statusCounts
            .Where(item => item.Status is TripStatus.Delivered or TripStatus.Closed)
            .Sum(item => item.Count);

        return new DispatchTripSummaryReport(
            statusCounts.OrderBy(item => item.Status.ToString()).ToList(),
            weeklyCounts,
            deliveredTrips,
            totalNonCancelledTrips,
            CalculatePercent(deliveredTrips, totalNonCancelledTrips));
    }

    public async Task<PagedQueryResult<DispatchDriverPerformanceReportItem>> GetDispatchDriverPerformanceReportAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var deliveredTrips = await GetDeliveredTripRowsAsync(fromUtc, toUtc, cancellationToken);
        await AttachDocumentComplianceAsync(deliveredTrips, cancellationToken);

        var rows = deliveredTrips
            .Where(trip => trip.DriverUserId.HasValue)
            .GroupBy(trip => new
            {
                DriverUserId = trip.DriverUserId!.Value,
                DriverName = string.IsNullOrWhiteSpace(trip.DriverName) ? "Unknown Driver" : trip.DriverName!
            })
            .Select(group =>
            {
                var durations = group
                    .Select(GetDeliveryDurationMinutes)
                    .Where(duration => duration.HasValue)
                    .Select(duration => duration!.Value)
                    .ToList();
                var scheduledTrips = group
                    .Where(trip => trip.DropoffScheduledAt.HasValue && trip.DeliveredAt.HasValue)
                    .ToList();
                var onTimeTrips = scheduledTrips
                    .Count(trip => trip.DeliveredAt!.Value <= trip.DropoffScheduledAt!.Value);
                var compliantTrips = group.Count(trip => trip.DocumentsCompliant);

                return new DispatchDriverPerformanceReportItem(
                    group.Key.DriverUserId,
                    group.Key.DriverName,
                    group.Count(),
                    durations.Count == 0 ? null : Math.Round((decimal)durations.Average(), 1),
                    CalculatePercent(onTimeTrips, scheduledTrips.Count),
                    CalculatePercent(compliantTrips, group.Count()));
            })
            .OrderByDescending(item => item.TripsCompleted)
            .ThenBy(item => item.DriverName)
            .ToList();

        return ToPaged(rows, page, pageSize);
    }

    public async Task<DispatchDeliveryTimeReport> GetDispatchDeliveryTimeReportAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var generatedAt = DateTime.UtcNow;
        var deliveredTrips = await GetDeliveredTripRowsAsync(fromUtc, toUtc, cancellationToken);

        var routeRows = deliveredTrips
            .Select(trip => new
            {
                Trip = trip,
                Duration = GetDeliveryDurationMinutes(trip)
            })
            .Where(item => item.Duration.HasValue)
            .GroupBy(item => new
            {
                FromLocation = NormalizeRouteLocation(item.Trip.PickupLocation),
                ToLocation = NormalizeRouteLocation(item.Trip.DropoffLocation)
            })
            .Where(group => !string.IsNullOrWhiteSpace(group.Key.FromLocation) && !string.IsNullOrWhiteSpace(group.Key.ToLocation))
            .Select(group =>
            {
                var durations = group.Select(item => item.Duration!.Value).ToList();
                return new DispatchDeliveryTimeRouteReportItem(
                    $"{group.Key.FromLocation} -> {group.Key.ToLocation}",
                    group.Key.FromLocation,
                    group.Key.ToLocation,
                    group.Count(),
                    Math.Round((decimal)durations.Average(), 1),
                    Math.Round((decimal)durations.Max(), 1),
                    generatedAt);
            })
            .OrderByDescending(item => item.TripCount)
            .ThenByDescending(item => item.AverageDeliveryMinutes)
            .ThenBy(item => item.RouteKey)
            .ToList();

        var longestRoutes = routeRows
            .OrderByDescending(item => item.AverageDeliveryMinutes)
            .ThenByDescending(item => item.TripCount)
            .Take(10)
            .ToList();

        return new DispatchDeliveryTimeReport(
            ToPaged(routeRows, page, pageSize),
            longestRoutes,
            generatedAt);
    }

    public async Task<IReadOnlyCollection<DispatchDocumentProcessingReportItem>> GetDispatchDocumentProcessingReportAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken = default)
    {
        var documentsQuery = _dbContext.DispatchTripDocuments
            .AsNoTracking()
            .AsQueryable();

        if (fromUtc.HasValue)
        {
            documentsQuery = documentsQuery.Where(document => document.UploadedAt >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            documentsQuery = documentsQuery.Where(document => document.UploadedAt <= toUtc.Value);
        }

        var documents = await documentsQuery
            .Select(document => new DispatchDocumentReportProjection
            {
                Type = document.Type,
                State = document.State,
                UploadedAt = document.UploadedAt,
                VerifiedAt = document.VerifiedAt
            })
            .ToListAsync(cancellationToken);

        var types = new[]
        {
            ("ATW", (TripDocumentType?)TripDocumentType.Atw),
            ("WAYBILL", TripDocumentType.Waybill),
            ("POD", TripDocumentType.Pod),
            ("EIR", null)
        };

        return types
            .Select(type =>
            {
                var docsForType = type.Item2.HasValue
                    ? documents.Where(document => document.Type == type.Item2.Value).ToList()
                    : [];
                var verifiedDocs = docsForType
                    .Where(document => document.State == TripDocumentState.Verified && document.VerifiedAt.HasValue)
                    .ToList();
                var verificationHours = verifiedDocs
                    .Select(document => (document.VerifiedAt!.Value - document.UploadedAt).TotalHours)
                    .Where(hours => hours >= 0)
                    .ToList();
                var rejectedCount = docsForType.Count(document => document.State == TripDocumentState.Rejected);

                return new DispatchDocumentProcessingReportItem(
                    type.Item1,
                    docsForType.Count(document => document.State == TripDocumentState.Uploaded),
                    verificationHours.Count == 0 ? null : Math.Round((decimal)verificationHours.Average(), 1),
                    CalculatePercent(rejectedCount, docsForType.Count));
            })
            .ToList();
    }

    public async Task<DispatchFinancialSummaryReport> GetDispatchFinancialSummaryReportAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        string groupBy,
        CancellationToken cancellationToken = default)
    {
        var financialTrips = await ApplyTripCreatedRange(_dbContext.DispatchTrips.AsNoTracking(), fromUtc, toUtc)
            .Where(trip => trip.Status != TripStatus.Cancelled)
            .Select(trip => new DispatchFinancialTripProjection
            {
                Id = trip.Id,
                CreatedAt = trip.CreatedAt,
                DriverUserId = trip.DriverUserId,
                DriverName = trip.Driver != null ? trip.Driver.Username : null,
                Status = trip.Status,
                Rate = trip.Rate,
                Payroll = trip.Payroll,
                FuelAmount = trip.FuelAmount,
                FuelPricePerLiter = trip.FuelPricePerLiter
            })
            .ToListAsync(cancellationToken);

        var periodRows = financialTrips
            .GroupBy(trip => groupBy.Equals("month", StringComparison.OrdinalIgnoreCase)
                ? GetMonthStart(trip.CreatedAt)
                : GetWeekStart(trip.CreatedAt))
            .Select(group => new DispatchFinancialPeriodReportItem(
                group.Key,
                groupBy.Equals("month", StringComparison.OrdinalIgnoreCase)
                    ? group.Key.ToString("yyyy-MM")
                    : group.Key.ToString("yyyy-MM-dd"),
                group.Sum(trip => trip.Rate ?? 0m),
                group.Sum(trip => trip.Payroll ?? 0m),
                group.Sum(CalculateFuelCost)))
            .OrderBy(item => item.PeriodStart)
            .ToList();

        var driverPayroll = financialTrips
            .Where(trip => trip.DriverUserId.HasValue)
            .GroupBy(trip => new
            {
                trip.DriverUserId,
                DriverName = string.IsNullOrWhiteSpace(trip.DriverName) ? "Unknown Driver" : trip.DriverName!
            })
            .Select(group => new DispatchDriverPayrollReportItem(
                group.Key.DriverUserId,
                group.Key.DriverName,
                group.Count(trip => trip.Status is TripStatus.Delivered or TripStatus.Closed),
                group.Sum(trip => trip.Payroll ?? 0m)))
            .OrderByDescending(item => item.TotalPayroll)
            .ThenBy(item => item.DriverName)
            .ToList();

        return new DispatchFinancialSummaryReport(periodRows, driverPayroll);
    }

    public async Task<PagedQueryResult<AuditLogItem>> GetAuditLogsAsync(
        string? action,
        string? entityType,
        Guid? entityId,
        DateTime? fromUtc,
        DateTime? toUtc,
        ClaimsPrincipal user,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var resolvedPage = page <= 0 ? 1 : page;
        var resolvedPageSize = pageSize <= 0 ? 20 : pageSize;

        var query = ApplyRoleFilter(_dbContext.AuditLogs.AsNoTracking(), user);

        if (!string.IsNullOrWhiteSpace(action))
        {
            var normalizedAction = action.Trim().ToUpperInvariant();
            query = query.Where(log => log.Action == normalizedAction);
        }

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            var normalized = entityType.Trim().ToLowerInvariant();
            query = query.Where(log => log.EntityType == normalized);
        }

        if (entityId.HasValue)
        {
            query = query.Where(log => log.EntityId == entityId.Value);
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(log => log.CreatedAt >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(log => log.CreatedAt <= toUtc.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var logs = await query
            .Include(log => log.Actor)
            .ThenInclude(actor => actor!.UserRoles)
            .ThenInclude(userRole => userRole.Role)
            .OrderByDescending(log => log.CreatedAt)
            .Skip((resolvedPage - 1) * resolvedPageSize)
            .Take(resolvedPageSize)
            .ToListAsync(cancellationToken);

        var items = logs
            .Select(log => new AuditLogItem(
                log.Id,
                log.Action,
                log.EntityType,
                log.EntityId,
                log.ActorUserId,
                log.Actor?.Username,
                ResolveActorRoleDisplay(log),
                log.CreatedAt,
                log.AfterJson ?? log.BeforeJson))
            .ToList();

        return new PagedQueryResult<AuditLogItem>(items, totalCount);
    }

    private static string? ResolveActorRoleDisplay(AuditLog log)
    {
        if (!string.IsNullOrWhiteSpace(log.ActorRole))
        {
            return log.ActorRole;
        }

        var roleNames = log.Actor?.UserRoles
            .Select(userRole => userRole.Role?.Name)
            .Where(roleName => !string.IsNullOrWhiteSpace(roleName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(roleName => roleName)
            .ToArray();

        return roleNames is { Length: > 0 }
            ? string.Join(",", roleNames)
            : null;
    }

    private IQueryable<AuditLog> ApplyRoleFilter(IQueryable<AuditLog> query, ClaimsPrincipal user)
    {
        if (HasRole(user, RoleNames.SuperAdmin) || HasRole(user, RoleNames.Admin))
        {
            return query;
        }

        var currentUserId = GetCurrentUserId(user);
        var ownActions = currentUserId.HasValue
            ? query.Where(log => log.ActorUserId == currentUserId.Value)
            : query.Where(log => false);

        if (HasRole(user, RoleNames.Manager) || HasRole(user, RoleNames.Dispatcher))
        {
            return query.Where(log =>
                log.EntityType == "trip"
                || log.EntityType == EntityTypes.DispatchTrip
                || log.EntityType == "shipment_request"
                || log.EntityType == "document"
                || log.EntityType == EntityTypes.DispatchTripDocument
                || log.EntityType == "assignment"
                || (currentUserId.HasValue && log.ActorUserId == currentUserId.Value));
        }

        if (HasRole(user, RoleNames.HeadOfFinance))
        {
            return query.Where(log =>
                ((log.EntityType == "trip"
                  || log.EntityType == EntityTypes.DispatchTrip
                  || log.EntityType == "payment"
                  || log.EntityType == "payment_proof")
                 && (log.Action == AuditActions.FinancialFieldAccessed
                     || log.Action == "RATE_UPDATED"
                     || log.Action == "PAYROLL_UPDATED"
                     || log.Action == "PAYMENT_RECORDED"))
                || (currentUserId.HasValue && log.ActorUserId == currentUserId.Value));
        }

        if (HasRole(user, RoleNames.InventoryOfficer))
        {
            return query.Where(log =>
                log.EntityType == "inventory_item"
                || log.EntityType == "borrow_request"
                || log.EntityType == "return_request"
                || log.EntityType == "stock_movement"
                || log.EntityType == EntityTypes.PurchaseOrder
                || (currentUserId.HasValue && log.ActorUserId == currentUserId.Value));
        }

        if (HasRole(user, RoleNames.Driver) && currentUserId.HasValue)
        {
            var assignedTripIds = _dbContext.DispatchTrips
                .AsNoTracking()
                .Where(trip => trip.DriverUserId == currentUserId.Value)
                .Select(trip => trip.Id);

            return query.Where(log =>
                log.ActorUserId == currentUserId.Value
                && assignedTripIds.Contains(log.EntityId));
        }

        return ownActions;
    }

    private static Guid? GetCurrentUserId(ClaimsPrincipal user)
    {
        var idValue = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
                      ?? user.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(idValue, out var userId)
            ? userId
            : null;
    }

    private static bool HasRole(ClaimsPrincipal user, string roleName)
    {
        return user.Claims.Any(claim =>
            (claim.Type == "role" || claim.Type == ClaimTypes.Role)
            && string.Equals(claim.Value, roleName, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<PagedQueryResult<AuthEventItem>> GetAuthEventsAsync(
        string? eventType,
        string? outcome,
        string? username,
        Guid? userId,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var resolvedPage = page <= 0 ? 1 : page;
        var resolvedPageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);

        var query = _dbContext.AuthEvents.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            var normalizedType = eventType.Trim().ToUpperInvariant();
            query = query.Where(ev => ev.EventType == normalizedType);
        }

        if (!string.IsNullOrWhiteSpace(outcome))
        {
            var normalizedOutcome = outcome.Trim().ToUpperInvariant();
            query = query.Where(ev => ev.Outcome == normalizedOutcome);
        }

        if (!string.IsNullOrWhiteSpace(username))
        {
            var normalizedUser = username.Trim();
            query = query.Where(ev => ev.Username == normalizedUser);
        }

        if (userId.HasValue)
        {
            query = query.Where(ev => ev.UserId == userId.Value);
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(ev => ev.CreatedAt >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(ev => ev.CreatedAt <= toUtc.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(ev => ev.CreatedAt)
            .Skip((resolvedPage - 1) * resolvedPageSize)
            .Take(resolvedPageSize)
            .Select(ev => new AuthEventItem(
                ev.Id,
                ev.EventType,
                ev.Outcome,
                ev.ReasonCode,
                ev.Username,
                ev.UserId,
                ev.RolesSnapshotJson,
                ev.AuthMethod,
                ev.MfaPerformed,
                ev.MfaMethod,
                ev.TokenJti,
                ev.CorrelationId,
                ev.IpAddress,
                ev.UserAgent,
                ev.ClientApp,
                ev.Environment,
                ev.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedQueryResult<AuthEventItem>(items, totalCount);
    }

    public async Task<IntegrityCheckResult> GetIntegrityCheckAsync(CancellationToken cancellationToken = default)
    {
        var maintenanceWithoutAsset = await _dbContext.Requests
            .AsNoTracking()
            .Where(request => request.RequestType == RequestType.MaintenanceIssue && !request.AssetId.HasValue)
            .CountAsync(cancellationToken);

        var loansWithNegativeRemaining = await _dbContext.LoanLines
            .AsNoTracking()
            .Where(line => (line.QtyIssued - line.QtyReturned) < 0)
            .CountAsync(cancellationToken);

        var purchaseOrdersOverReceived = await _dbContext.PurchaseOrderLines
            .AsNoTracking()
            .Where(line => line.QtyReceived > line.QtyOrdered)
            .CountAsync(cancellationToken);

        var inventoryNegativeQuantity = await _dbContext.InventoryItems
            .AsNoTracking()
            .Where(item => item.Quantity < 0)
            .CountAsync(cancellationToken);

        var requestsIssuedWithoutStockLogs = await _dbContext.Requests
            .AsNoTracking()
            .Where(request =>
                (request.RequestType == RequestType.MaintenanceIssue || request.RequestType == RequestType.Borrow)
                && (request.Status == RequestStatus.Issued || request.Status == RequestStatus.Closed))
            .Where(request => !_dbContext.StockLogs.Any(log =>
                log.RefType == EntityTypes.Request
                && log.RefId == request.Id
                && (log.MovementType == StockMovementType.Out || log.MovementType == StockMovementType.Borrow)))
            .CountAsync(cancellationToken);

        var duplicateIssueLogs = await _dbContext.StockLogs
            .AsNoTracking()
            .Where(log => log.RefType == EntityTypes.Request
                          && (log.MovementType == StockMovementType.Out || log.MovementType == StockMovementType.Borrow))
            .GroupBy(log => new { log.RefId, log.InventoryId, log.MovementType })
            .Where(group => group.Count() > 1)
            .CountAsync(cancellationToken);

        var orphanStockLogs = await _dbContext.StockLogs
            .AsNoTracking()
            .Where(log =>
                (log.RefType == EntityTypes.Request
                 && !_dbContext.Requests.Any(request => request.Id == log.RefId))
                || (log.RefType == EntityTypes.Loan
                    && !_dbContext.Loans.Any(loan => loan.Id == log.RefId))
                || (log.RefType == EntityTypes.PurchaseOrder
                    && !_dbContext.PurchaseOrders.Any(order => order.Id == log.RefId))
                || (log.RefType == EntityTypes.InventoryAdjustment
                    && !_dbContext.InventoryAdjustments.Any(adjustment => adjustment.Id == log.RefId)))
            .CountAsync(cancellationToken);

        var orphanApprovalActions = await _dbContext.ApprovalActions
            .AsNoTracking()
            .Where(action => !_dbContext.Approvals.Any(approval => approval.Id == action.ApprovalId))
            .CountAsync(cancellationToken);

        var poWithoutWorkflow = await _dbContext.PurchaseOrders
            .AsNoTracking()
            .Where(order => order.Status != PurchaseOrderStatus.Draft)
            .Where(order => !_dbContext.Approvals.Any(approval =>
                approval.EntityType == EntityTypes.PurchaseOrder
                && approval.EntityId == order.Id))
            .CountAsync(cancellationToken);

        var adjustmentWithoutLogs = await _dbContext.InventoryAdjustments
            .AsNoTracking()
            .Where(adjustment => adjustment.Status == InventoryAdjustmentStatus.Approved)
            .Where(adjustment => !_dbContext.StockLogs.Any(log =>
                log.RefType == EntityTypes.InventoryAdjustment
                && log.RefId == adjustment.Id
                && log.MovementType == StockMovementType.Adjustment))
            .CountAsync(cancellationToken);

        var supplierMissingCreateAudit = await _dbContext.Suppliers
            .AsNoTracking()
            .Where(supplier => !_dbContext.AuditLogs.Any(log =>
                log.EntityType == EntityTypes.Supplier
                && log.EntityId == supplier.Id
                && log.Action == AuditActions.SupplierCreated))
            .CountAsync(cancellationToken);

        var supplierMissingDeactivateAudit = await _dbContext.Suppliers
            .AsNoTracking()
            .Where(supplier => !supplier.IsActive)
            .Where(supplier => !_dbContext.AuditLogs.Any(log =>
                log.EntityType == EntityTypes.Supplier
                && log.EntityId == supplier.Id
                && log.Action == AuditActions.SupplierDeactivated))
            .CountAsync(cancellationToken);

        var poReceiptsWithoutAudit = await _dbContext.PurchaseOrderReceipts
            .AsNoTracking()
            .Where(receipt => !_dbContext.AuditLogs.Any(log =>
                log.EntityType == EntityTypes.PurchaseOrderReceipt
                && log.EntityId == receipt.Id))
            .CountAsync(cancellationToken);

        var loanReturnsWithoutAudit = await _dbContext.LoanLineReturns
            .AsNoTracking()
            .Where(ret => !_dbContext.AuditLogs.Any(log =>
                log.EntityType == EntityTypes.LoanReturn
                && log.EntityId == ret.Id))
            .CountAsync(cancellationToken);

        var adjustmentsWithoutAudit = await _dbContext.InventoryAdjustments
            .AsNoTracking()
            .Where(adjustment => !_dbContext.AuditLogs.Any(log =>
                log.EntityType == EntityTypes.InventoryAdjustment
                && log.EntityId == adjustment.Id))
            .CountAsync(cancellationToken);

        var inactiveSuppliersReferenced = await (
                from order in _dbContext.PurchaseOrders.AsNoTracking()
                join supplier in _dbContext.Suppliers.AsNoTracking() on order.SupplierId equals supplier.Id
                where !supplier.IsActive
                select order.Id)
            .CountAsync(cancellationToken);

        var inactiveInventoryIds = _dbContext.InventoryItems
            .AsNoTracking()
            .Where(item => !item.IsActive)
            .Select(item => item.Id);

        var inactiveInventoryReferenced =
            await _dbContext.RequestLines
                .AsNoTracking()
                .Where(line => inactiveInventoryIds.Contains(line.InventoryId))
                .CountAsync(cancellationToken)
            + await _dbContext.PurchaseOrderLines
                .AsNoTracking()
                .Where(line => inactiveInventoryIds.Contains(line.InventoryId))
                .CountAsync(cancellationToken)
            + await _dbContext.LoanLines
                .AsNoTracking()
                .Where(line => inactiveInventoryIds.Contains(line.InventoryId))
                .CountAsync(cancellationToken)
            + await _dbContext.InventoryAdjustmentLines
                .AsNoTracking()
                .Where(line => inactiveInventoryIds.Contains(line.InventoryId))
                .CountAsync(cancellationToken);

        return new IntegrityCheckResult(
            maintenanceWithoutAsset,
            loansWithNegativeRemaining,
            purchaseOrdersOverReceived,
            inventoryNegativeQuantity,
            requestsIssuedWithoutStockLogs,
            duplicateIssueLogs,
            orphanStockLogs,
            orphanApprovalActions,
            poWithoutWorkflow,
            adjustmentWithoutLogs,
            supplierMissingCreateAudit + supplierMissingDeactivateAudit,
            poReceiptsWithoutAudit,
            loanReturnsWithoutAudit,
            adjustmentsWithoutAudit,
            inactiveSuppliersReferenced,
            inactiveInventoryReferenced);
    }

    public async Task<IReadOnlyCollection<AssetConsumptionSummary>> GetAssetConsumptionReportAsync(
        Guid? assetId,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken = default)
    {
        var requestsQuery = _dbContext.Requests
            .AsNoTracking()
            .Where(request =>
                request.RequestType == RequestType.MaintenanceIssue
                && request.Status == RequestStatus.Closed
                && request.AssetId.HasValue);

        if (assetId.HasValue)
        {
            requestsQuery = requestsQuery.Where(request => request.AssetId == assetId.Value);
        }

        var logsQuery = _dbContext.StockLogs
            .AsNoTracking()
            .Where(log =>
                log.RefType == EntityTypes.Request
                && log.MovementType == StockMovementType.Out);

        if (fromUtc.HasValue)
        {
            logsQuery = logsQuery.Where(log => log.CreatedAt >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            logsQuery = logsQuery.Where(log => log.CreatedAt <= toUtc.Value);
        }

        var aggregated = await (
                from log in logsQuery
                join request in requestsQuery on log.RefId equals request.Id
                join asset in _dbContext.Assets.AsNoTracking() on request.AssetId equals asset.Id
                join item in _dbContext.InventoryItems.AsNoTracking() on log.InventoryId equals item.Id
                select new
                {
                    asset.Id,
                    asset.AssetCode,
                    InventoryId = item.Id,
                    InventoryName = item.Name,
                    QtyConsumed = Math.Abs(log.QtyDelta),
                    Cost = log.TotalCostSnapshot ?? 0m
                })
            .GroupBy(entry => new
            {
                entry.Id,
                entry.AssetCode,
                entry.InventoryId,
                entry.InventoryName
            })
            .Select(group => new
            {
                group.Key.Id,
                group.Key.AssetCode,
                group.Key.InventoryId,
                group.Key.InventoryName,
                TotalItemsConsumed = group.Sum(entry => entry.QtyConsumed),
                TotalCost = group.Sum(entry => entry.Cost)
            })
            .ToListAsync(cancellationToken);

        return aggregated
            .GroupBy(entry => new { entry.Id, entry.AssetCode })
            .Select(group => new AssetConsumptionSummary(
                group.Key.Id,
                group.Key.AssetCode,
                group
                    .OrderByDescending(entry => entry.TotalItemsConsumed)
                    .ThenBy(entry => entry.InventoryName)
                    .Take(5)
                    .Select(entry => new AssetConsumptionItem(
                        entry.InventoryId,
                        entry.InventoryName,
                        entry.TotalItemsConsumed,
                        entry.TotalCost))
                    .ToList()))
            .OrderBy(entry => entry.AssetCode)
            .ToList();
    }

    private async Task<List<DispatchDeliveredTripProjection>> GetDeliveredTripRowsAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken)
    {
        var rows = await _dbContext.DispatchTrips
            .AsNoTracking()
            .Where(trip =>
                trip.Status == TripStatus.Delivered
                || trip.Status == TripStatus.Closed
                || trip.StatusHistory.Any(history => history.ToStatus == TripStatus.Delivered))
            .Select(trip => new DispatchDeliveredTripProjection
            {
                Id = trip.Id,
                DriverUserId = trip.DriverUserId,
                DriverName = trip.Driver != null ? trip.Driver.Username : null,
                Status = trip.Status,
                CreatedAt = trip.CreatedAt,
                UpdatedAt = trip.UpdatedAt,
                StartedAt = trip.StatusHistory
                    .Where(history =>
                        history.ToStatus == TripStatus.Dispatched
                        || history.ToStatus == TripStatus.EnroutePickup
                        || history.ToStatus == TripStatus.AtPickup
                        || history.ToStatus == TripStatus.Loaded)
                    .OrderBy(history => history.EventAt)
                    .Select(history => (DateTime?)history.EventAt)
                    .FirstOrDefault(),
                DeliveredAt = trip.StatusHistory
                    .Where(history => history.ToStatus == TripStatus.Delivered)
                    .OrderByDescending(history => history.EventAt)
                    .Select(history => (DateTime?)history.EventAt)
                    .FirstOrDefault(),
                PickupLocation = trip.Stops
                    .Where(stop => stop.StopType == TripStopType.Pickup)
                    .Select(stop => stop.LocationText)
                    .FirstOrDefault(),
                DropoffLocation = trip.Stops
                    .Where(stop => stop.StopType == TripStopType.Dropoff)
                    .Select(stop => stop.LocationText)
                    .FirstOrDefault(),
                PickupScheduledAt = trip.Stops
                    .Where(stop => stop.StopType == TripStopType.Pickup)
                    .Select(stop => stop.ScheduledAt)
                    .FirstOrDefault(),
                DropoffScheduledAt = trip.Stops
                    .Where(stop => stop.StopType == TripStopType.Dropoff)
                    .Select(stop => stop.ScheduledAt)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        foreach (var row in rows)
        {
            if (!row.DeliveredAt.HasValue && row.Status is TripStatus.Delivered or TripStatus.Closed)
            {
                row.DeliveredAt = row.UpdatedAt ?? row.CreatedAt;
            }
        }

        return rows
            .Where(row => row.DeliveredAt.HasValue)
            .Where(row => !fromUtc.HasValue || row.DeliveredAt!.Value >= fromUtc.Value)
            .Where(row => !toUtc.HasValue || row.DeliveredAt!.Value <= toUtc.Value)
            .ToList();
    }

    private async Task AttachDocumentComplianceAsync(
        IReadOnlyCollection<DispatchDeliveredTripProjection> trips,
        CancellationToken cancellationToken)
    {
        if (trips.Count == 0)
        {
            return;
        }

        if (_requiredDispatchDocumentTypes.Length == 0)
        {
            foreach (var trip in trips)
            {
                trip.DocumentsCompliant = true;
            }
            return;
        }

        var tripIds = trips.Select(trip => trip.Id).ToArray();
        var documents = await _dbContext.DispatchTripDocuments
            .AsNoTracking()
            .Where(document =>
                tripIds.Contains(document.TripId)
                && document.IsActive
                && _requiredDispatchDocumentTypes.Contains(document.Type))
            .Select(document => new DispatchDocumentComplianceProjection
            {
                TripId = document.TripId,
                Type = document.Type,
                State = document.State
            })
            .ToListAsync(cancellationToken);

        var documentsByTrip = documents
            .GroupBy(document => document.TripId)
            .ToDictionary(group => group.Key, group => group.ToList());

        foreach (var trip in trips)
        {
            documentsByTrip.TryGetValue(trip.Id, out var tripDocuments);
            trip.DocumentsCompliant = _requiredDispatchDocumentTypes.All(requiredType =>
                tripDocuments?.Any(document =>
                    document.Type == requiredType
                    && document.State == TripDocumentState.Verified) == true);
        }
    }

    private static IQueryable<Trip> ApplyTripCreatedRange(IQueryable<Trip> query, DateTime? fromUtc, DateTime? toUtc)
    {
        if (fromUtc.HasValue)
        {
            query = query.Where(trip => trip.CreatedAt >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(trip => trip.CreatedAt <= toUtc.Value);
        }

        return query;
    }

    private static PagedQueryResult<T> ToPaged<T>(IReadOnlyCollection<T> items, int page, int pageSize)
    {
        var resolvedPage = page <= 0 ? 1 : page;
        var resolvedPageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);

        return new PagedQueryResult<T>(
            items
                .Skip((resolvedPage - 1) * resolvedPageSize)
                .Take(resolvedPageSize)
                .ToList(),
            items.Count);
    }

    private static double? GetDeliveryDurationMinutes(DispatchDeliveredTripProjection trip)
    {
        var start = trip.StartedAt ?? trip.CreatedAt;
        if (!trip.DeliveredAt.HasValue || trip.DeliveredAt.Value <= start)
        {
            return null;
        }

        return (trip.DeliveredAt.Value - start).TotalMinutes;
    }

    private static DateTime GetWeekStart(DateTime value)
    {
        var date = value.Date;
        var offset = date.DayOfWeek == DayOfWeek.Sunday
            ? 6
            : (int)date.DayOfWeek - (int)DayOfWeek.Monday;
        return date.AddDays(-offset);
    }

    private static DateTime GetMonthStart(DateTime value)
    {
        return new DateTime(value.Year, value.Month, 1, 0, 0, 0, value.Kind);
    }

    private static decimal CalculateFuelCost(DispatchFinancialTripProjection trip)
    {
        if (!trip.FuelAmount.HasValue)
        {
            return 0m;
        }

        return trip.FuelPricePerLiter.HasValue
            ? trip.FuelAmount.Value * trip.FuelPricePerLiter.Value
            : trip.FuelAmount.Value;
    }

    private static decimal CalculatePercent(int numerator, int denominator)
    {
        return denominator == 0
            ? 0m
            : Math.Round((decimal)numerator / denominator * 100m, 1);
    }

    private static string NormalizeRouteLocation(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
    }

    private sealed class DispatchDeliveredTripProjection
    {
        public Guid Id { get; init; }
        public Guid? DriverUserId { get; init; }
        public string? DriverName { get; init; }
        public TripStatus Status { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }
        public DateTime? StartedAt { get; init; }
        public DateTime? DeliveredAt { get; set; }
        public string? PickupLocation { get; init; }
        public string? DropoffLocation { get; init; }
        public DateTime? PickupScheduledAt { get; init; }
        public DateTime? DropoffScheduledAt { get; init; }
        public bool DocumentsCompliant { get; set; }
    }

    private sealed class DispatchDocumentComplianceProjection
    {
        public Guid TripId { get; init; }
        public TripDocumentType Type { get; init; }
        public TripDocumentState State { get; init; }
    }

    private sealed class DispatchDocumentReportProjection
    {
        public TripDocumentType Type { get; init; }
        public TripDocumentState State { get; init; }
        public DateTime UploadedAt { get; init; }
        public DateTime? VerifiedAt { get; init; }
    }

    private sealed class DispatchFinancialTripProjection
    {
        public Guid Id { get; init; }
        public DateTime CreatedAt { get; init; }
        public Guid? DriverUserId { get; init; }
        public string? DriverName { get; init; }
        public TripStatus Status { get; init; }
        public decimal? Rate { get; init; }
        public decimal? Payroll { get; init; }
        public decimal? FuelAmount { get; init; }
        public decimal? FuelPricePerLiter { get; init; }
    }
}
