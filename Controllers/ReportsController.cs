using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NVGInventory.Contracts;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Enums;
using NVGInventory.Domain.Services;

namespace NVGInventory.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize]
public sealed class ReportsController : ControllerBase
{
    private readonly ReportQueryService _reportQueryService;

    public ReportsController(ReportQueryService reportQueryService)
    {
        _reportQueryService = reportQueryService;
    }

    [HttpGet("stock-movements")]
    [Authorize(Roles = $"{RoleNames.InventoryOfficer},{RoleNames.Manager},{RoleNames.Admin},{RoleNames.SuperAdmin}")]
    public async Task<ActionResult<IReadOnlyCollection<StockMovementReportItemResponse>>> GetStockMovements(
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] Guid? inventoryId,
        CancellationToken cancellationToken)
    {
        if (!TryParseUtcDate(from, out var fromUtc, out var fromError))
        {
            return BadRequest(fromError);
        }

        if (!TryParseUtcDate(to, out var toUtc, out var toError))
        {
            return BadRequest(toError);
        }

        if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
        {
            return BadRequest("'from' must be earlier than or equal to 'to'.");
        }

        var results = await _reportQueryService.GetStockMovementReportAsync(fromUtc, toUtc, inventoryId, cancellationToken);
        var response = results
            .Select(item => new StockMovementReportItemResponse(
                item.InventoryId,
                item.InventoryName,
                item.TotalIn,
                item.TotalOut,
                item.TotalBorrow,
                item.TotalReturn,
                item.TotalAdjustment))
            .ToList();

        return Ok(response);
    }

    [HttpGet("low-stock")]
    [Authorize(Roles = $"{RoleNames.InventoryOfficer},{RoleNames.Manager},{RoleNames.Admin},{RoleNames.SuperAdmin}")]
    public async Task<ActionResult<IReadOnlyCollection<LowStockReportItemResponse>>> GetLowStock(
        CancellationToken cancellationToken)
    {
        var results = await _reportQueryService.GetLowStockReportAsync(cancellationToken);
        var response = results
            .Select(item => new LowStockReportItemResponse(
                item.InventoryId,
                item.InventoryName,
                item.Quantity,
                item.ReorderLevel))
            .ToList();

        return Ok(response);
    }

    [HttpGet("open-loans")]
    [Authorize(Roles = $"{RoleNames.InventoryOfficer},{RoleNames.Manager},{RoleNames.Admin},{RoleNames.SuperAdmin}")]
    public async Task<ActionResult<IReadOnlyCollection<OpenLoanReportItemResponse>>> GetOpenLoans(
        CancellationToken cancellationToken)
    {
        var results = await _reportQueryService.GetOpenLoanReportAsync(cancellationToken);
        var response = results
            .Select(item => new OpenLoanReportItemResponse(
                item.LoanId,
                item.BorrowerUsername,
                item.AssetCode,
                item.TotalItemsBorrowed,
                item.TotalItemsReturned,
                item.Status,
                item.IssuedAt))
            .ToList();

        return Ok(response);
    }

    [HttpGet("inventory-valuation")]
    [Authorize(Roles = $"{RoleNames.InventoryOfficer},{RoleNames.Manager},{RoleNames.Admin},{RoleNames.SuperAdmin}")]
    public async Task<ActionResult<IReadOnlyCollection<InventoryValuationReportItemResponse>>> GetInventoryValuation(
        CancellationToken cancellationToken)
    {
        var results = await _reportQueryService.GetInventoryValuationReportAsync(cancellationToken);
        var response = results
            .Select(item => new InventoryValuationReportItemResponse(
                item.InventoryId,
                item.InventoryName,
                item.Quantity,
                item.AverageCost,
                item.TotalValue))
            .ToList();

        return Ok(response);
    }

    [HttpGet("inventory-valuation.csv")]
    [Authorize(Roles = $"{RoleNames.InventoryOfficer},{RoleNames.Manager},{RoleNames.Admin},{RoleNames.SuperAdmin}")]
    public async Task<IActionResult> ExportInventoryValuationCsv(CancellationToken cancellationToken)
    {
        var results = await _reportQueryService.GetInventoryValuationReportAsync(cancellationToken);
        var rows = new List<string[]>
        {
            new[] { "InventoryId", "InventoryName", "Quantity", "AverageCost", "TotalValue" }
        };

        foreach (var item in results)
        {
            rows.Add(new[]
            {
                item.InventoryId.ToString(),
                item.InventoryName,
                item.Quantity.ToString(CultureInfo.InvariantCulture),
                item.AverageCost.ToString(CultureInfo.InvariantCulture),
                item.TotalValue.ToString(CultureInfo.InvariantCulture)
            });
        }

        var csv = BuildCsv(rows);
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", "inventory-valuation.csv");
    }

    [HttpGet("asset-maintenance-cost")]
    [Authorize(Roles = $"{RoleNames.InventoryOfficer},{RoleNames.Manager},{RoleNames.Admin},{RoleNames.SuperAdmin}")]
    public async Task<ActionResult<PagedResult<AssetMaintenanceCostReportItemResponse>>> GetAssetMaintenanceCost(
        [FromQuery] Guid? assetId,
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        if (!TryParseUtcDate(from, out var fromUtc, out var fromError))
        {
            return BadRequest(fromError);
        }

        if (!TryParseUtcDate(to, out var toUtc, out var toError))
        {
            return BadRequest(toError);
        }

        if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
        {
            return BadRequest("'from' must be earlier than or equal to 'to'.");
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

        var results = await _reportQueryService.GetAssetMaintenanceCostReportAsync(
            assetId,
            fromUtc,
            toUtc,
            resolvedPage,
            resolvedPageSize,
            cancellationToken);

        var responseItems = results.Items
            .Select(item => new AssetMaintenanceCostReportItemResponse(
                item.AssetId,
                item.AssetCode,
                item.TotalMaintenanceCost,
                item.TotalItemsConsumed,
                item.RequestCount))
            .ToList();

        var response = new PagedResult<AssetMaintenanceCostReportItemResponse>(
            responseItems,
            results.TotalCount,
            resolvedPage,
            resolvedPageSize);

        return Ok(response);
    }

    [HttpGet("asset-maintenance-cost.csv")]
    [Authorize(Roles = $"{RoleNames.InventoryOfficer},{RoleNames.Manager},{RoleNames.Admin},{RoleNames.SuperAdmin}")]
    public async Task<IActionResult> ExportAssetMaintenanceCostCsv(
        [FromQuery] Guid? assetId,
        [FromQuery] string? from,
        [FromQuery] string? to,
        CancellationToken cancellationToken)
    {
        if (!TryParseUtcDate(from, out var fromUtc, out var fromError))
        {
            return BadRequest(fromError);
        }

        if (!TryParseUtcDate(to, out var toUtc, out var toError))
        {
            return BadRequest(toError);
        }

        if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
        {
            return BadRequest("'from' must be earlier than or equal to 'to'.");
        }

        var rows = new List<string[]>
        {
            new[] { "AssetId", "AssetCode", "TotalMaintenanceCost", "TotalItemsConsumed", "RequestCount" }
        };

        const int pageSize = 500;
        var page = 1;
        int totalCount;
        do
        {
            var result = await _reportQueryService.GetAssetMaintenanceCostReportAsync(
                assetId,
                fromUtc,
                toUtc,
                page,
                pageSize,
                cancellationToken);

            totalCount = result.TotalCount;
            foreach (var item in result.Items)
            {
                rows.Add(new[]
                {
                    item.AssetId.ToString(),
                    item.AssetCode,
                    item.TotalMaintenanceCost.ToString(CultureInfo.InvariantCulture),
                    item.TotalItemsConsumed.ToString(CultureInfo.InvariantCulture),
                    item.RequestCount.ToString(CultureInfo.InvariantCulture)
                });
            }

            page++;
        } while ((page - 1) * pageSize < totalCount);

        var csv = BuildCsv(rows);
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", "asset-maintenance-cost.csv");
    }

    [HttpGet("asset-consumption")]
    [Authorize(Roles = $"{RoleNames.InventoryOfficer},{RoleNames.Manager},{RoleNames.Admin},{RoleNames.SuperAdmin}")]
    public async Task<ActionResult<IReadOnlyCollection<AssetConsumptionBreakdownItemResponse>>> GetAssetConsumption(
        [FromQuery] Guid? assetId,
        [FromQuery] string? from,
        [FromQuery] string? to,
        CancellationToken cancellationToken)
    {
        if (!assetId.HasValue)
        {
            return BadRequest("assetId is required.");
        }

        if (!TryParseUtcDate(from, out var fromUtc, out var fromError))
        {
            return BadRequest(fromError);
        }

        if (!TryParseUtcDate(to, out var toUtc, out var toError))
        {
            return BadRequest(toError);
        }

        if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
        {
            return BadRequest("'from' must be earlier than or equal to 'to'.");
        }

        var results = await _reportQueryService.GetAssetConsumptionBreakdownAsync(
            assetId.Value,
            fromUtc,
            toUtc,
            cancellationToken);

        var response = results
            .Select(item => new AssetConsumptionBreakdownItemResponse(
                item.InventoryId,
                item.InventoryName,
                item.TotalQuantityUsed,
                item.TotalCost))
            .ToList();

        return Ok(response);
    }

    [HttpGet("integrity/maintenance-without-asset")]
    [Authorize(Roles = $"{RoleNames.Manager},{RoleNames.HeadOfFinance},{RoleNames.Ceo},{RoleNames.Admin},{RoleNames.SuperAdmin}")]
    public async Task<ActionResult<MaintenanceWithoutAssetIntegrityResponse>> GetMaintenanceWithoutAssetCount(
        CancellationToken cancellationToken)
    {
        var count = await _reportQueryService.GetMaintenanceIssuesWithoutAssetCountAsync(cancellationToken);
        return Ok(new MaintenanceWithoutAssetIntegrityResponse(count));
    }

    [HttpGet("adjustments")]
    [Authorize(Roles = $"{RoleNames.InventoryOfficer},{RoleNames.Manager},{RoleNames.Admin},{RoleNames.SuperAdmin}")]
    public async Task<ActionResult<IReadOnlyCollection<AdjustmentReportItemResponse>>> GetAdjustments(
        [FromQuery] string? from,
        [FromQuery] string? to,
        CancellationToken cancellationToken)
    {
        if (!TryParseUtcDate(from, out var fromUtc, out var fromError))
        {
            return BadRequest(fromError);
        }

        if (!TryParseUtcDate(to, out var toUtc, out var toError))
        {
            return BadRequest(toError);
        }

        if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
        {
            return BadRequest("'from' must be earlier than or equal to 'to'.");
        }

        var results = await _reportQueryService.GetAdjustmentReportAsync(fromUtc, toUtc, cancellationToken);
        var response = results
            .Select(item => new AdjustmentReportItemResponse(
                item.RequestId,
                item.Reason,
                item.InventoryId,
                item.InventoryName,
                item.Quantity,
                item.ActorUserId,
                item.ActorUsername,
                item.CreatedAt))
            .ToList();

        return Ok(response);
    }

    [HttpGet("supplier-spend")]
    [Authorize(Roles = $"{RoleNames.Manager},{RoleNames.HeadOfFinance},{RoleNames.Ceo},{RoleNames.Admin},{RoleNames.SuperAdmin}")]
    public async Task<ActionResult<PagedResult<SupplierSpendReportItemResponse>>> GetSupplierSpend(
        [FromQuery] string? basis,
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        if (!TryParseUtcDate(from, out var fromUtc, out var fromError))
        {
            return BadRequest(fromError);
        }

        if (!TryParseUtcDate(to, out var toUtc, out var toError))
        {
            return BadRequest(toError);
        }

        if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
        {
            return BadRequest("'from' must be earlier than or equal to 'to'.");
        }

        var parsedBasis = SupplierSpendBasis.Ordered;
        if (!string.IsNullOrWhiteSpace(basis))
        {
            if (!QueryParsing.TryParseEnum(basis, out parsedBasis))
            {
                return BadRequest("Invalid basis. Use ORDERED or RECEIVED.");
            }
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

        var results = await _reportQueryService.GetSupplierSpendReportAsync(
            parsedBasis,
            fromUtc,
            toUtc,
            resolvedPage,
            resolvedPageSize,
            cancellationToken);

        var responseItems = results.Items
            .Select(item => new SupplierSpendReportItemResponse(
                parsedBasis.ToString().ToUpperInvariant(),
                item.SupplierId,
                item.SupplierName,
                item.TotalSpend,
                item.TotalPurchaseOrders,
                item.TotalLines,
                item.TotalQty,
                item.AveragePurchaseOrderValue))
            .ToList();

        var response = new PagedResult<SupplierSpendReportItemResponse>(
            responseItems,
            results.TotalCount,
            resolvedPage,
            resolvedPageSize);

        return Ok(response);
    }

    [HttpGet("audit")]
    [Authorize(Roles = $"{RoleNames.SuperAdmin},{RoleNames.Admin},{RoleNames.Manager},{RoleNames.Dispatcher},{RoleNames.HeadOfFinance},{RoleNames.InventoryOfficer},{RoleNames.Driver}")]
    public async Task<ActionResult<PagedResult<AuditLogItemResponse>>> GetAuditLogs(
        [FromQuery] string? action,
        [FromQuery] string? entityType,
        [FromQuery] Guid? entityId,
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseUtcDate(from, out var fromUtc, out var fromError))
        {
            return BadRequest(fromError);
        }

        if (!TryParseUtcDate(to, out var toUtc, out var toError))
        {
            return BadRequest(toError);
        }

        if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
        {
            return BadRequest("'from' must be earlier than or equal to 'to'.");
        }

        var results = await _reportQueryService.GetAuditLogsAsync(
            action,
            entityType,
            entityId,
            fromUtc,
            toUtc,
            User,
            page,
            pageSize,
            cancellationToken);

        var response = new PagedResult<AuditLogItemResponse>(
            results.Items.Select(item => new AuditLogItemResponse(
                item.Id,
                item.Action,
                item.EntityType,
                item.EntityId,
                item.ActorUserId,
                item.ActorUsername,
                item.ActorRole,
                item.CreatedAt,
                item.Metadata)).ToList(),
            results.TotalCount,
            page <= 0 ? 1 : page,
            pageSize <= 0 ? 20 : Math.Min(pageSize, 100));

        return Ok(response);
    }

    [HttpGet("auth-events")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.SuperAdmin}")]
    public async Task<ActionResult<PagedResult<AuthEventItemResponse>>> GetAuthEvents(
        [FromQuery] string? eventType,
        [FromQuery] string? outcome,
        [FromQuery] string? username,
        [FromQuery] Guid? userId,
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseUtcDate(from, out var fromUtc, out var fromError))
        {
            return BadRequest(fromError);
        }

        if (!TryParseUtcDate(to, out var toUtc, out var toError))
        {
            return BadRequest(toError);
        }

        if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
        {
            return BadRequest("'from' must be earlier than or equal to 'to'.");
        }

        var results = await _reportQueryService.GetAuthEventsAsync(
            eventType,
            outcome,
            username,
            userId,
            fromUtc,
            toUtc,
            page,
            pageSize,
            cancellationToken);

        var response = new PagedResult<AuthEventItemResponse>(
            results.Items.Select(item => new AuthEventItemResponse(
                item.Id,
                item.EventType,
                item.Outcome,
                item.ReasonCode,
                item.Username,
                item.UserId,
                item.RolesSnapshotJson,
                item.AuthMethod,
                item.MfaPerformed,
                item.MfaMethod,
                item.TokenJti,
                item.CorrelationId,
                item.IpAddress,
                item.UserAgent,
                item.ClientApp,
                item.Environment,
                item.CreatedAt)).ToList(),
            results.TotalCount,
            page <= 0 ? 1 : page,
            pageSize <= 0 ? 20 : Math.Min(pageSize, 100));

        return Ok(response);
    }

    [HttpGet("supplier-spend.csv")]
    [Authorize(Roles = $"{RoleNames.Manager},{RoleNames.HeadOfFinance},{RoleNames.Ceo},{RoleNames.Admin},{RoleNames.SuperAdmin}")]
    public async Task<IActionResult> ExportSupplierSpendCsv(
        [FromQuery] string? basis,
        [FromQuery] string? from,
        [FromQuery] string? to,
        CancellationToken cancellationToken)
    {
        if (!TryParseUtcDate(from, out var fromUtc, out var fromError))
        {
            return BadRequest(fromError);
        }

        if (!TryParseUtcDate(to, out var toUtc, out var toError))
        {
            return BadRequest(toError);
        }

        if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
        {
            return BadRequest("'from' must be earlier than or equal to 'to'.");
        }

        var parsedBasis = SupplierSpendBasis.Ordered;
        if (!string.IsNullOrWhiteSpace(basis))
        {
            if (!QueryParsing.TryParseEnum(basis, out parsedBasis))
            {
                return BadRequest("Invalid basis. Use ORDERED or RECEIVED.");
            }
        }

        var rows = new List<string[]>
        {
            new[]
            {
                "Basis",
                "SupplierId",
                "SupplierName",
                "TotalSpend",
                "TotalPurchaseOrders",
                "TotalLines",
                "TotalQty",
                "AveragePurchaseOrderValue"
            }
        };

        const int pageSize = 500;
        var page = 1;
        int totalCount;
        do
        {
            var results = await _reportQueryService.GetSupplierSpendReportAsync(
                parsedBasis,
                fromUtc,
                toUtc,
                page,
                pageSize,
                cancellationToken);

            totalCount = results.TotalCount;
            foreach (var supplier in results.Items)
            {
                rows.Add(new[]
                {
                    parsedBasis.ToString().ToUpperInvariant(),
                    supplier.SupplierId.ToString(),
                    supplier.SupplierName,
                    supplier.TotalSpend.ToString(CultureInfo.InvariantCulture),
                    supplier.TotalPurchaseOrders.ToString(CultureInfo.InvariantCulture),
                    supplier.TotalLines.ToString(CultureInfo.InvariantCulture),
                    supplier.TotalQty.ToString(CultureInfo.InvariantCulture),
                    supplier.AveragePurchaseOrderValue.ToString(CultureInfo.InvariantCulture)
                });
            }

            page++;
        } while ((page - 1) * pageSize < totalCount);

        var csv = BuildCsv(rows);
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", "supplier-spend.csv");
    }

    [HttpGet("integrity-check")]
    [Authorize(Roles = $"{RoleNames.Manager},{RoleNames.HeadOfFinance},{RoleNames.Ceo},{RoleNames.Admin},{RoleNames.SuperAdmin}")]
    public async Task<ActionResult<IntegrityCheckResponse>> GetIntegrityCheck(
        CancellationToken cancellationToken)
    {
        var result = await _reportQueryService.GetIntegrityCheckAsync(cancellationToken);
        return Ok(new IntegrityCheckResponse(
            result.MaintenanceWithoutAsset,
            result.LoansWithNegativeRemaining,
            result.PurchaseOrdersOverReceived,
            result.InventoryNegativeQuantity,
            result.RequestsIssuedWithoutStockLogs,
            result.DuplicateIssueLogs,
            result.OrphanStockLogs,
            result.OrphanApprovalActions,
            result.PoWithoutWorkflow,
            result.AdjustmentWithoutLogs,
            result.SupplierActionsWithoutAudit,
            result.PoReceiptsWithoutAudit,
            result.LoanReturnsWithoutAudit,
            result.AdjustmentsWithoutAudit,
            result.InactiveSuppliersReferenced,
            result.InactiveInventoryReferenced));
    }

    [HttpGet("integrity")]
    [Authorize(Roles = $"{RoleNames.Manager},{RoleNames.HeadOfFinance},{RoleNames.Ceo},{RoleNames.Admin},{RoleNames.SuperAdmin}")]
    public async Task<ActionResult<IntegritySummaryResponse>> GetIntegritySummary(
        CancellationToken cancellationToken)
    {
        var result = await _reportQueryService.GetIntegrityCheckAsync(cancellationToken);
        return Ok(new IntegritySummaryResponse(
            result.MaintenanceWithoutAsset,
            result.LoansWithNegativeRemaining,
            result.PurchaseOrdersOverReceived,
            result.InventoryNegativeQuantity,
            result.RequestsIssuedWithoutStockLogs,
            result.DuplicateIssueLogs,
            result.OrphanStockLogs,
            result.OrphanApprovalActions,
            result.PoWithoutWorkflow,
            result.AdjustmentWithoutLogs,
            result.SupplierActionsWithoutAudit,
            result.PoReceiptsWithoutAudit,
            result.LoanReturnsWithoutAudit,
            result.AdjustmentsWithoutAudit,
            result.InactiveSuppliersReferenced,
            result.InactiveInventoryReferenced));
    }

    private static bool TryParseUtcDate(string? raw, out DateTime? parsed, out string? error)
    {
        parsed = null;
        error = null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        if (!DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value))
        {
            error = "Invalid date value.";
            return false;
        }

        parsed = value.UtcDateTime;
        return true;
    }

    private static string BuildCsv(IEnumerable<string[]> rows)
    {
        var sb = new StringBuilder();
        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(",", row.Select(EscapeCsv)));
        }
        return sb.ToString();
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var needsQuotes = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
        if (!needsQuotes)
        {
            return value;
        }

        var escaped = value.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }
}
