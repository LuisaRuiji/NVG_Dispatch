using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NVGInventory.Domain.Services;

namespace NVGInventory.BackgroundJobs;

public sealed class IntegrityCheckHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IntegrityCheckHostedService> _logger;
    private readonly IOptions<IntegrityCheckJobOptions> _options;

    public IntegrityCheckHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<IntegrityCheckHostedService> logger,
        IOptions<IntegrityCheckJobOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = _options.Value;
        if (!options.Enabled)
        {
            _logger.LogInformation("Integrity check background job disabled.");
            return;
        }

        var intervalMinutes = options.IntervalMinutes <= 0 ? 60 : options.IntervalMinutes;
        var delay = TimeSpan.FromMinutes(intervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var reportQuery = scope.ServiceProvider.GetRequiredService<ReportQueryService>();
                var result = await reportQuery.GetIntegrityCheckAsync(stoppingToken);
                var totalIssues = SumIssues(result);

                if (totalIssues == 0)
                {
                    _logger.LogInformation("Integrity check passed.");
                }
                else
                {
                    _logger.LogWarning(
                        "Integrity check detected {TotalIssues} issue(s): {@Result}",
                        totalIssues,
                        result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Integrity check background job failed.");
            }

            await Task.Delay(delay, stoppingToken);
        }
    }

    private static int SumIssues(IntegrityCheckResult result)
    {
        return result.MaintenanceWithoutAsset
               + result.LoansWithNegativeRemaining
               + result.PurchaseOrdersOverReceived
               + result.InventoryNegativeQuantity
               + result.RequestsIssuedWithoutStockLogs
               + result.DuplicateIssueLogs
               + result.OrphanStockLogs
               + result.OrphanApprovalActions
               + result.PoWithoutWorkflow
               + result.AdjustmentWithoutLogs
               + result.SupplierActionsWithoutAudit
               + result.PoReceiptsWithoutAudit
               + result.LoanReturnsWithoutAudit
               + result.AdjustmentsWithoutAudit
               + result.InactiveSuppliersReferenced
               + result.InactiveInventoryReferenced;
    }
}
