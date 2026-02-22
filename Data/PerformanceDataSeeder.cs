using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Enums;
using NVGInventory.Domain.Services;

namespace NVGInventory.Data;

public sealed record PerformanceSeedOptions(
    int TotalRequests,
    int PendingManagerCount,
    int ApprovedCount);

public sealed class PerformanceDataSeeder
{
    private readonly InventoryDbContext _dbContext;
    private readonly RequestWorkflowService _requestWorkflow;
    private readonly DemoDataSeeder _demoSeeder;
    private readonly IHostEnvironment _environment;

    public PerformanceDataSeeder(
        InventoryDbContext dbContext,
        RequestWorkflowService requestWorkflow,
        DemoDataSeeder demoSeeder,
        IHostEnvironment environment)
    {
        _dbContext = dbContext;
        _requestWorkflow = requestWorkflow;
        _demoSeeder = demoSeeder;
        _environment = environment;
    }

    public async Task SeedAsync(
        PerformanceSeedOptions options,
        bool resetDatabase = true,
        CancellationToken cancellationToken = default)
    {
        if (!_environment.IsDevelopment())
        {
            throw new InvalidOperationException("Performance seeding allowed only in Development.");
        }

        await _demoSeeder.SeedAsync(resetDatabase, cancellationToken);

        var totalRequests = Math.Max(0, options.TotalRequests);
        if (totalRequests == 0)
        {
            return;
        }

        var pendingManagerCount = Math.Clamp(options.PendingManagerCount, 0, totalRequests);
        var approvedCount = Math.Clamp(options.ApprovedCount, 0, totalRequests - pendingManagerCount);

        var driver = await _dbContext.Users
            .AsNoTracking()
            .FirstAsync(user => user.Username == "drv_demo", cancellationToken);
        var ioUser = await _dbContext.Users
            .AsNoTracking()
            .FirstAsync(user => user.Username == "io_demo", cancellationToken);
        var manager = await _dbContext.Users
            .AsNoTracking()
            .FirstAsync(user => user.Username == "mgr_demo", cancellationToken);
        var asset = await _dbContext.Assets
            .AsNoTracking()
            .FirstAsync(a => a.AssetCode == "TRK-001", cancellationToken);
        var inventoryItem = await _dbContext.InventoryItems
            .AsNoTracking()
            .FirstAsync(item => item.Name == "Engine Oil", cancellationToken);

        var existingPurposes = await _dbContext.Requests
            .AsNoTracking()
            .Where(request => request.Purpose != null && request.Purpose.StartsWith("PERF_MAINT_"))
            .Select(request => request.Purpose!)
            .ToListAsync(cancellationToken);

        var usedPurposes = new HashSet<string>(existingPurposes, StringComparer.OrdinalIgnoreCase);

        var created = 0;
        var sequence = 1;
        while (created < totalRequests)
        {
            var purpose = $"PERF_MAINT_{sequence:0000}";
            sequence++;
            if (!usedPurposes.Add(purpose))
            {
                continue;
            }

            var submit = await _requestWorkflow.SubmitMaintenanceIssueAsync(
                new SubmitMaintenanceIssueCommand(
                    driver.Id,
                    asset.Id,
                    purpose,
                    new[]
                    {
                        new RequestLineInput(inventoryItem.Id, 1m, null)
                    }),
                cancellationToken);

            if (created < pendingManagerCount + approvedCount)
            {
                var lines = await _dbContext.RequestLines
                    .AsNoTracking()
                    .Where(line => line.RequestId == submit.RequestId)
                    .Select(line => new { line.Id, line.QtyRequested })
                    .ToListAsync(cancellationToken);

                await _requestWorkflow.InventoryOfficerReviewAsync(
                    submit.RequestId,
                    ioUser.Id,
                    lines.Select(line => (line.Id, line.QtyRequested, (string?)null)).ToList(),
                    "Perf review",
                    cancellationToken);

                if (created < approvedCount)
                {
                    await _requestWorkflow.ManagerDecisionAsync(
                        submit.RequestId,
                        manager.Id,
                        ApprovalDecision.Approve,
                        "Perf approve",
                        cancellationToken);
                }
            }

            created++;
        }
    }
}
