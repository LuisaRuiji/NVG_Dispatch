using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NVGInventory.Contracts;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Services;
using NVGInventory.Modules.Dispatching.Entities;
using NVGInventory.Modules.Dispatching.Enums;
using NVGInventory.Security;
using NVGInventory.Services;

namespace NVGInventory.Controllers;

[ApiController]
[Route("api/reports/dispatch")]
[Authorize]
public sealed class DispatchReportsController : ControllerBase
{
    private readonly InventoryDbContext _dbContext;
    private readonly ReportQueryService _reportQueryService;
    private readonly IAuditService _auditService;
    private readonly IVaiaCacheService _cacheService;

    public DispatchReportsController(
        InventoryDbContext dbContext,
        ReportQueryService reportQueryService,
        IAuditService auditService,
        IVaiaCacheService cacheService)
    {
        _dbContext = dbContext;
        _reportQueryService = reportQueryService;
        _auditService = auditService;
        _cacheService = cacheService;
    }

    [HttpGet("trip-summary")]
    [Authorize(Roles = $"{RoleNames.Dispatcher},{RoleNames.Manager},{RoleNames.Admin},{RoleNames.SuperAdmin},{RoleNames.Ceo}")]
    public async Task<ActionResult<DispatchTripSummaryReportResponse>> GetTripSummary(
        [FromQuery] string? from,
        [FromQuery] string? to,
        CancellationToken cancellationToken)
    {
        if (!TryResolveDateRange(from, to, out var fromUtc, out var toUtc, out var rangeError))
        {
            return BadRequest(rangeError);
        }

        var response = await _cacheService.GetOrSetAsync(
            VaiaCacheKeys.ReportKey(VaiaCacheKeys.TripSummaryReport, fromUtc, toUtc),
            async () =>
            {
                var result = await _reportQueryService.GetDispatchTripSummaryReportAsync(fromUtc, toUtc, cancellationToken);
                return new DispatchTripSummaryReportResponse(
                    result.StatusCounts
                        .Select(item => new DispatchTripStatusCountReportItemResponse(item.Status, item.Count))
                        .ToList(),
                    result.WeeklyCounts
                        .Select(item => new DispatchTripWeeklyCountReportItemResponse(item.WeekStart, item.Count))
                        .ToList(),
                    result.DeliveredTrips,
                    result.TotalNonCancelledTrips,
                    result.CompletionRatePercent);
            },
            TimeSpan.FromMinutes(5),
            cancellationToken);
        return Ok(response);
    }

    [HttpGet("trip-summary.csv")]
    [Authorize(Roles = $"{RoleNames.Dispatcher},{RoleNames.Manager},{RoleNames.Admin},{RoleNames.SuperAdmin},{RoleNames.Ceo}")]
    public async Task<IActionResult> ExportTripSummaryCsv(
        [FromQuery] string? from,
        [FromQuery] string? to,
        CancellationToken cancellationToken)
    {
        if (!TryResolveDateRange(from, to, out var fromUtc, out var toUtc, out var rangeError))
        {
            return BadRequest(rangeError);
        }

        var result = await _reportQueryService.GetDispatchTripSummaryReportAsync(fromUtc, toUtc, cancellationToken);
        var rows = new List<string[]>
        {
            new[] { "Section", "Key", "Count", "Value" }
        };

        foreach (var item in result.StatusCounts)
        {
            rows.Add(new[] { "Status", item.Status.ToString(), item.Count.ToString(CultureInfo.InvariantCulture), string.Empty });
        }

        foreach (var item in result.WeeklyCounts)
        {
            rows.Add(new[] { "Week", item.WeekStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), item.Count.ToString(CultureInfo.InvariantCulture), string.Empty });
        }

        rows.Add(new[] { "Completion", "DeliveredTrips", result.DeliveredTrips.ToString(CultureInfo.InvariantCulture), string.Empty });
        rows.Add(new[] { "Completion", "TotalNonCancelledTrips", result.TotalNonCancelledTrips.ToString(CultureInfo.InvariantCulture), string.Empty });
        rows.Add(new[] { "Completion", "CompletionRatePercent", string.Empty, result.CompletionRatePercent.ToString(CultureInfo.InvariantCulture) });

        return File(Encoding.UTF8.GetBytes(BuildCsv(rows)), "text/csv", "dispatch-trip-summary.csv");
    }

    [HttpGet("driver-performance")]
    [Authorize(Roles = $"{RoleNames.Manager},{RoleNames.Admin},{RoleNames.SuperAdmin},{RoleNames.Ceo}")]
    public async Task<ActionResult<PagedResult<DispatchDriverPerformanceReportItemResponse>>> GetDriverPerformance(
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        if (!TryResolveDateRange(from, to, out var fromUtc, out var toUtc, out var rangeError))
        {
            return BadRequest(rangeError);
        }

        if (!TryResolvePaging(page, pageSize, out var resolvedPage, out var resolvedPageSize, out var pagingError))
        {
            return BadRequest(pagingError);
        }

        var response = await _cacheService.GetOrSetAsync(
            VaiaCacheKeys.ReportKey(
                VaiaCacheKeys.DriverPerformanceReport,
                fromUtc,
                toUtc,
                resolvedPage,
                resolvedPageSize),
            async () =>
            {
                var result = await _reportQueryService.GetDispatchDriverPerformanceReportAsync(
                    fromUtc,
                    toUtc,
                    resolvedPage,
                    resolvedPageSize,
                    cancellationToken);

                return new PagedResult<DispatchDriverPerformanceReportItemResponse>(
                    result.Items.Select(MapDriverPerformance).ToList(),
                    result.TotalCount,
                    resolvedPage,
                    resolvedPageSize);
            },
            TimeSpan.FromMinutes(5),
            cancellationToken);

        return Ok(response);
    }

    [HttpGet("driver-performance.csv")]
    [Authorize(Roles = $"{RoleNames.Manager},{RoleNames.Admin},{RoleNames.SuperAdmin},{RoleNames.Ceo}")]
    public async Task<IActionResult> ExportDriverPerformanceCsv(
        [FromQuery] string? from,
        [FromQuery] string? to,
        CancellationToken cancellationToken)
    {
        if (!TryResolveDateRange(from, to, out var fromUtc, out var toUtc, out var rangeError))
        {
            return BadRequest(rangeError);
        }

        var rows = new List<string[]>
        {
            new[] { "DriverUserId", "DriverName", "TripsCompleted", "AverageDeliveryMinutes", "OnTimeRatePercent", "DocumentComplianceRatePercent" }
        };

        const int exportPageSize = 500;
        var page = 1;
        int totalCount;
        do
        {
            var result = await _reportQueryService.GetDispatchDriverPerformanceReportAsync(
                fromUtc,
                toUtc,
                page,
                exportPageSize,
                cancellationToken);

            totalCount = result.TotalCount;
            foreach (var item in result.Items)
            {
                rows.Add(new[]
                {
                    item.DriverUserId.ToString(),
                    item.DriverName,
                    item.TripsCompleted.ToString(CultureInfo.InvariantCulture),
                    item.AverageDeliveryMinutes?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    item.OnTimeRatePercent.ToString(CultureInfo.InvariantCulture),
                    item.DocumentComplianceRatePercent.ToString(CultureInfo.InvariantCulture)
                });
            }

            page++;
        } while ((page - 1) * exportPageSize < totalCount);

        return File(Encoding.UTF8.GetBytes(BuildCsv(rows)), "text/csv", "dispatch-driver-performance.csv");
    }

    [HttpGet("delivery-time")]
    [Authorize(Roles = $"{RoleNames.Manager},{RoleNames.Admin},{RoleNames.SuperAdmin},{RoleNames.Ceo}")]
    public async Task<ActionResult<DispatchDeliveryTimeReportResponse>> GetDeliveryTime(
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        if (!TryResolveDateRange(from, to, out var fromUtc, out var toUtc, out var rangeError))
        {
            return BadRequest(rangeError);
        }

        if (!TryResolvePaging(page, pageSize, out var resolvedPage, out var resolvedPageSize, out var pagingError))
        {
            return BadRequest(pagingError);
        }

        var response = await _cacheService.GetOrSetAsync(
            VaiaCacheKeys.ReportKey(
                VaiaCacheKeys.DeliveryTimeReport,
                fromUtc,
                toUtc,
                resolvedPage,
                resolvedPageSize),
            async () =>
            {
                var result = await _reportQueryService.GetDispatchDeliveryTimeReportAsync(
                    fromUtc,
                    toUtc,
                    resolvedPage,
                    resolvedPageSize,
                    cancellationToken);

                return new DispatchDeliveryTimeReportResponse(
                    new PagedResult<DispatchDeliveryTimeRouteReportItemResponse>(
                        result.Routes.Items.Select(MapDeliveryTimeRoute).ToList(),
                        result.Routes.TotalCount,
                        resolvedPage,
                        resolvedPageSize),
                    result.LongestRoutes.Select(MapDeliveryTimeRoute).ToList(),
                    result.GeneratedAt);
            },
            TimeSpan.FromMinutes(5),
            cancellationToken);

        return Ok(response);
    }

    [HttpGet("delivery-time.csv")]
    [Authorize(Roles = $"{RoleNames.Manager},{RoleNames.Admin},{RoleNames.SuperAdmin},{RoleNames.Ceo}")]
    public async Task<IActionResult> ExportDeliveryTimeCsv(
        [FromQuery] string? from,
        [FromQuery] string? to,
        CancellationToken cancellationToken)
    {
        if (!TryResolveDateRange(from, to, out var fromUtc, out var toUtc, out var rangeError))
        {
            return BadRequest(rangeError);
        }

        var rows = new List<string[]>
        {
            new[] { "RouteKey", "FromLocation", "ToLocation", "TripCount", "AverageDeliveryMinutes", "LongestDeliveryMinutes", "GeneratedAt" }
        };

        const int exportPageSize = 500;
        var page = 1;
        int totalCount;
        do
        {
            var result = await _reportQueryService.GetDispatchDeliveryTimeReportAsync(
                fromUtc,
                toUtc,
                page,
                exportPageSize,
                cancellationToken);

            totalCount = result.Routes.TotalCount;
            foreach (var item in result.Routes.Items)
            {
                rows.Add(new[]
                {
                    item.RouteKey,
                    item.FromLocation,
                    item.ToLocation,
                    item.TripCount.ToString(CultureInfo.InvariantCulture),
                    item.AverageDeliveryMinutes.ToString(CultureInfo.InvariantCulture),
                    item.LongestDeliveryMinutes.ToString(CultureInfo.InvariantCulture),
                    item.GeneratedAt.ToString("O", CultureInfo.InvariantCulture)
                });
            }

            page++;
        } while ((page - 1) * exportPageSize < totalCount);

        return File(Encoding.UTF8.GetBytes(BuildCsv(rows)), "text/csv", "dispatch-delivery-time.csv");
    }

    [HttpGet("document-processing")]
    [Authorize(Roles = $"{RoleNames.Manager},{RoleNames.Admin},{RoleNames.SuperAdmin},{RoleNames.Ceo},{RoleNames.Dispatcher}")]
    public async Task<ActionResult<IReadOnlyCollection<DispatchDocumentProcessingReportItemResponse>>> GetDocumentProcessing(
        [FromQuery] string? from,
        [FromQuery] string? to,
        CancellationToken cancellationToken)
    {
        if (!TryResolveDateRange(from, to, out var fromUtc, out var toUtc, out var rangeError))
        {
            return BadRequest(rangeError);
        }

        var result = await _reportQueryService.GetDispatchDocumentProcessingReportAsync(fromUtc, toUtc, cancellationToken);
        return Ok(result.Select(MapDocumentProcessing).ToList());
    }

    [HttpGet("document-processing.csv")]
    [Authorize(Roles = $"{RoleNames.Manager},{RoleNames.Admin},{RoleNames.SuperAdmin},{RoleNames.Ceo},{RoleNames.Dispatcher}")]
    public async Task<IActionResult> ExportDocumentProcessingCsv(
        [FromQuery] string? from,
        [FromQuery] string? to,
        CancellationToken cancellationToken)
    {
        if (!TryResolveDateRange(from, to, out var fromUtc, out var toUtc, out var rangeError))
        {
            return BadRequest(rangeError);
        }

        var result = await _reportQueryService.GetDispatchDocumentProcessingReportAsync(fromUtc, toUtc, cancellationToken);
        var rows = new List<string[]>
        {
            new[] { "DocumentType", "PendingVerification", "AverageVerificationHours", "RejectionRatePercent" }
        };

        foreach (var item in result)
        {
            rows.Add(new[]
            {
                item.DocumentType,
                item.PendingVerification.ToString(CultureInfo.InvariantCulture),
                item.AverageVerificationHours?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                item.RejectionRatePercent.ToString(CultureInfo.InvariantCulture)
            });
        }

        return File(Encoding.UTF8.GetBytes(BuildCsv(rows)), "text/csv", "dispatch-document-processing.csv");
    }

    [HttpGet("financial-summary")]
    [Authorize(Roles = $"{RoleNames.HeadOfFinance},{RoleNames.Manager},{RoleNames.Admin},{RoleNames.SuperAdmin},{RoleNames.Ceo}")]
    public async Task<ActionResult<DispatchFinancialSummaryReportResponse>> GetFinancialSummary(
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] string? groupBy,
        CancellationToken cancellationToken)
    {
        if (!TryResolveDateRange(from, to, out var fromUtc, out var toUtc, out var rangeError))
        {
            return BadRequest(rangeError);
        }

        var normalizedGroupBy = NormalizeFinancialGroupBy(groupBy);
        var result = await _reportQueryService.GetDispatchFinancialSummaryReportAsync(
            fromUtc,
            toUtc,
            normalizedGroupBy,
            cancellationToken);

        await RecordFinancialAccessAsync(
            "reports/dispatch/financial-summary",
            false,
            fromUtc,
            toUtc,
            normalizedGroupBy,
            cancellationToken);

        return Ok(MapFinancialSummary(result));
    }

    [HttpGet("financial-summary.csv")]
    [Authorize(Roles = $"{RoleNames.HeadOfFinance},{RoleNames.Manager},{RoleNames.Admin},{RoleNames.SuperAdmin},{RoleNames.Ceo}")]
    public async Task<IActionResult> ExportFinancialSummaryCsv(
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] string? groupBy,
        CancellationToken cancellationToken)
    {
        if (!TryResolveDateRange(from, to, out var fromUtc, out var toUtc, out var rangeError))
        {
            return BadRequest(rangeError);
        }

        var normalizedGroupBy = NormalizeFinancialGroupBy(groupBy);
        var result = await _reportQueryService.GetDispatchFinancialSummaryReportAsync(
            fromUtc,
            toUtc,
            normalizedGroupBy,
            cancellationToken);

        var rows = new List<string[]>
        {
            new[] { "Section", "PeriodOrDriverId", "Label", "DeliveredTrips", "TripRevenue", "DriverPayroll", "FuelCost" }
        };

        foreach (var period in result.Periods)
        {
            rows.Add(new[]
            {
                "Period",
                period.PeriodStart.ToString("O", CultureInfo.InvariantCulture),
                period.PeriodLabel,
                string.Empty,
                period.TotalTripRevenue.ToString(CultureInfo.InvariantCulture),
                period.TotalDriverPayroll.ToString(CultureInfo.InvariantCulture),
                period.TotalFuelCost.ToString(CultureInfo.InvariantCulture)
            });
        }

        foreach (var driver in result.DriverPayroll)
        {
            rows.Add(new[]
            {
                "DriverPayroll",
                driver.DriverUserId?.ToString() ?? string.Empty,
                driver.DriverName,
                driver.DeliveredTrips.ToString(CultureInfo.InvariantCulture),
                string.Empty,
                driver.TotalPayroll.ToString(CultureInfo.InvariantCulture),
                string.Empty
            });
        }

        await RecordFinancialAccessAsync(
            "reports/dispatch/financial-summary.csv",
            true,
            fromUtc,
            toUtc,
            normalizedGroupBy,
            cancellationToken);

        return File(Encoding.UTF8.GetBytes(BuildCsv(rows)), "text/csv", "dispatch-financial-summary.csv");
    }

    [HttpGet("recommendations")]
    [Authorize(Roles = $"{RoleNames.Manager},{RoleNames.Admin},{RoleNames.SuperAdmin},{RoleNames.Ceo}")]
    public async Task<ActionResult<DispatchRecommendationReportResponse>> GetRecommendations(
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        if (!TryResolveDateRange(from, to, out var fromUtc, out var toUtc, out var rangeError))
        {
            return BadRequest(rangeError);
        }

        if (!TryResolvePaging(page, pageSize, out var resolvedPage, out var resolvedPageSize, out var pagingError))
        {
            return BadRequest(pagingError);
        }

        var query = ApplyRecommendationDateRange(
            LoadRecommendationReportQuery(),
            fromUtc,
            toUtc);

        var allItems = await query.ToListAsync(cancellationToken);
        var totalGenerated = allItems.Count;
        var totalAccepted = allItems.Count(item => item.WasAccepted);
        var totalIgnored = allItems.Count(item => item.WasIgnored);
        var acceptanceRate = totalGenerated == 0
            ? 0m
            : Math.Round(totalAccepted * 100m / totalGenerated, 2, MidpointRounding.AwayFromZero);
        var averageAcceptedScore = totalAccepted == 0
            ? (decimal?)null
            : Math.Round(allItems.Where(item => item.WasAccepted).Average(item => item.TotalScore), 4, MidpointRounding.AwayFromZero);
        var weekly = allItems
            .GroupBy(item => GetUtcWeekStart(item.GeneratedAt))
            .OrderBy(group => group.Key)
            .Select(group => new DispatchRecommendationWeeklyReportItemResponse(
                group.Key,
                group.Count(item => item.WasAccepted),
                group.Count(item => item.WasIgnored)))
            .ToList();

        var totalHistory = allItems.Count(item => item.WasAccepted || item.WasIgnored);
        var historyItems = allItems
            .Where(item => item.WasAccepted || item.WasIgnored)
            .OrderByDescending(item => item.GeneratedAt)
            .ThenBy(item => item.Rank)
            .Skip((resolvedPage - 1) * resolvedPageSize)
            .Take(resolvedPageSize)
            .Select(MapRecommendationHistory)
            .ToList();

        return Ok(new DispatchRecommendationReportResponse(
            new DispatchRecommendationKpiReportResponse(
                totalGenerated,
                totalAccepted,
                totalIgnored,
                acceptanceRate,
                averageAcceptedScore),
            weekly,
            new PagedResult<DispatchRecommendationHistoryReportItemResponse>(
                historyItems,
                totalHistory,
                resolvedPage,
                resolvedPageSize)));
    }

    [HttpGet("recommendations.csv")]
    [Authorize(Roles = $"{RoleNames.Manager},{RoleNames.Admin},{RoleNames.SuperAdmin},{RoleNames.Ceo}")]
    public async Task<IActionResult> ExportRecommendationsCsv(
        [FromQuery] string? from,
        [FromQuery] string? to,
        CancellationToken cancellationToken)
    {
        if (!TryResolveDateRange(from, to, out var fromUtc, out var toUtc, out var rangeError))
        {
            return BadRequest(rangeError);
        }

        var items = await ApplyRecommendationDateRange(
                LoadRecommendationReportQuery(),
                fromUtc,
                toUtc)
            .Where(item => item.WasAccepted || item.WasIgnored)
            .ToListAsync(cancellationToken);
        var rows = new List<string[]>
        {
            new[] { "Date", "Driver", "CurrentMovementId", "SuggestedNextMovementId", "Action", "ReviewedBy" }
        };

        foreach (var item in items.OrderByDescending(item => item.GeneratedAt).ThenBy(item => item.Rank))
        {
            var mapped = MapRecommendationHistory(item);
            rows.Add(new[]
            {
                mapped.Date.ToString("O", CultureInfo.InvariantCulture),
                mapped.Driver,
                mapped.CompletedTripId.ToString(),
                mapped.RecommendedTripId.ToString(),
                mapped.Action == "Accepted" ? "Confirmed" : mapped.Action == "Ignored" ? "Dismissed" : mapped.Action,
                mapped.ReviewedBy ?? string.Empty
            });
        }

        return File(Encoding.UTF8.GetBytes(BuildCsv(rows)), "text/csv", "trip-chaining-history.csv");
    }

    private async Task RecordFinancialAccessAsync(
        string endpoint,
        bool isCsvExport,
        DateTime? fromUtc,
        DateTime? toUtc,
        string groupBy,
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
                IsCsvExport = isCsvExport,
                From = fromUtc,
                To = toUtc,
                GroupBy = groupBy,
                Fields = new[] { "Rate", "Payroll", "FuelAmount", "FuelPricePerLiter" }
            });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static DispatchDriverPerformanceReportItemResponse MapDriverPerformance(
        DispatchDriverPerformanceReportItem item)
    {
        return new DispatchDriverPerformanceReportItemResponse(
            item.DriverUserId,
            item.DriverName,
            item.TripsCompleted,
            item.AverageDeliveryMinutes,
            item.OnTimeRatePercent,
            item.DocumentComplianceRatePercent);
    }

    private static DispatchDeliveryTimeRouteReportItemResponse MapDeliveryTimeRoute(
        DispatchDeliveryTimeRouteReportItem item)
    {
        return new DispatchDeliveryTimeRouteReportItemResponse(
            item.RouteKey,
            item.FromLocation,
            item.ToLocation,
            item.TripCount,
            item.AverageDeliveryMinutes,
            item.LongestDeliveryMinutes,
            item.GeneratedAt);
    }

    private static DispatchDocumentProcessingReportItemResponse MapDocumentProcessing(
        DispatchDocumentProcessingReportItem item)
    {
        return new DispatchDocumentProcessingReportItemResponse(
            item.DocumentType,
            item.PendingVerification,
            item.AverageVerificationHours,
            item.RejectionRatePercent);
    }

    private static DispatchFinancialSummaryReportResponse MapFinancialSummary(
        DispatchFinancialSummaryReport result)
    {
        return new DispatchFinancialSummaryReportResponse(
            result.Periods
                .Select(item => new DispatchFinancialPeriodReportItemResponse(
                    item.PeriodStart,
                    item.PeriodLabel,
                    item.TotalTripRevenue,
                    item.TotalDriverPayroll,
                    item.TotalFuelCost))
                .ToList(),
            result.DriverPayroll
                .Select(item => new DispatchDriverPayrollReportItemResponse(
                    item.DriverUserId,
                    item.DriverName,
                    item.DeliveredTrips,
                    item.TotalPayroll))
                .ToList());
    }

    private IQueryable<DispatchRecommendation> LoadRecommendationReportQuery()
    {
        return _dbContext.DispatchRecommendations
            .Include(recommendation => recommendation.CompletedTrip)
                .ThenInclude(trip => trip.Driver)
            .Include(recommendation => recommendation.CompletedTrip)
                .ThenInclude(trip => trip.Stops)
            .Include(recommendation => recommendation.RecommendedTrip)
                .ThenInclude(trip => trip.Stops)
            .Include(recommendation => recommendation.ReviewedByUser)
            .AsSplitQuery();
    }

    private static IQueryable<DispatchRecommendation> ApplyRecommendationDateRange(
        IQueryable<DispatchRecommendation> query,
        DateTime? fromUtc,
        DateTime? toUtc)
    {
        if (fromUtc.HasValue)
        {
            query = query.Where(recommendation => recommendation.GeneratedAt >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(recommendation => recommendation.GeneratedAt <= toUtc.Value);
        }

        return query;
    }

    private static DispatchRecommendationHistoryReportItemResponse MapRecommendationHistory(
        DispatchRecommendation recommendation)
    {
        return new DispatchRecommendationHistoryReportItemResponse(
            recommendation.GeneratedAt,
            recommendation.CompletedTrip.Driver?.Username ?? "Unassigned driver",
            recommendation.CompletedTripId,
            recommendation.RecommendedTripId,
            recommendation.TotalScore,
            recommendation.Rank,
            recommendation.WasAccepted ? "Accepted" : "Ignored",
            recommendation.ReviewedByUser?.Username);
    }

    private static DateTime GetUtcWeekStart(DateTime value)
    {
        var date = value.Date;
        var offset = date.DayOfWeek == DayOfWeek.Sunday
            ? 6
            : (int)date.DayOfWeek - (int)DayOfWeek.Monday;

        return date.AddDays(-offset);
    }

    private static bool TryResolveDateRange(
        string? from,
        string? to,
        out DateTime? fromUtc,
        out DateTime? toUtc,
        out string? error)
    {
        fromUtc = null;
        toUtc = null;

        if (!TryParseUtcDate(from, out fromUtc, out error))
        {
            return false;
        }

        if (!TryParseUtcDate(to, out toUtc, out error))
        {
            return false;
        }

        if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
        {
            error = "'from' must be earlier than or equal to 'to'.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryResolvePaging(
        int? page,
        int? pageSize,
        out int resolvedPage,
        out int resolvedPageSize,
        out string? error)
    {
        resolvedPage = page.GetValueOrDefault(1);
        if (resolvedPage < 1)
        {
            error = "Page must be at least 1.";
            resolvedPageSize = 0;
            return false;
        }

        resolvedPageSize = pageSize.GetValueOrDefault(20);
        if (resolvedPageSize < 1 || resolvedPageSize > 100)
        {
            error = "Page size must be between 1 and 100.";
            return false;
        }

        error = null;
        return true;
    }

    private static string NormalizeFinancialGroupBy(string? groupBy)
    {
        return string.Equals(groupBy, "month", StringComparison.OrdinalIgnoreCase)
            ? "month"
            : "week";
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
