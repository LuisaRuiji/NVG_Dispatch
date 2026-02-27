
using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Enums;

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

public sealed record AuditLogItem(
    Guid Id,
    string Action,
    string EntityType,
    Guid EntityId,
    Guid ActorUserId,
    string? ActorUsername,
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

    private sealed class SupplierSpendAggregateProjection
    {
        public Guid SupplierId { get; init; }
        public string SupplierName { get; init; } = string.Empty;
        public decimal TotalSpend { get; init; }
        public decimal TotalQty { get; init; }
        public int TotalLines { get; init; }
        public int TotalPurchaseOrders { get; init; }
    }

    public ReportQueryService(InventoryDbContext dbContext)
    {
        _dbContext = dbContext;
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

    public async Task<PagedQueryResult<AuditLogItem>> GetAuditLogsAsync(
        string? action,
        string? entityType,
        Guid? entityId,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var resolvedPage = page <= 0 ? 1 : page;
        var resolvedPageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);

        var query = _dbContext.AuditLogs.AsNoTracking();

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

        var items = await query
            .OrderByDescending(log => log.CreatedAt)
            .Skip((resolvedPage - 1) * resolvedPageSize)
            .Take(resolvedPageSize)
            .Select(log => new AuditLogItem(
                log.Id,
                log.Action,
                log.EntityType,
                log.EntityId,
                log.ActorUserId,
                log.Actor != null ? log.Actor.Username : null,
                log.CreatedAt,
                log.AfterJson ?? log.BeforeJson))
            .ToListAsync(cancellationToken);

        return new PagedQueryResult<AuditLogItem>(items, totalCount);
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
}
