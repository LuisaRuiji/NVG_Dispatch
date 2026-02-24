using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Entities;
using NVGInventory.Domain.Enums;
using NVGInventory.Domain.Services;

namespace NVGInventory.Data;

public sealed class DemoDataSeeder
{
    public const string SuperAdminUsername = "Superadmin";
    public const string SuperAdminEmail = "Superadmin@nvg.com";
    public const string SuperAdminPassword = "Super123!";

    private readonly InventoryDbContext _dbContext;
    private readonly UserService _userService;
    private readonly AssetService _assetService;
    private readonly InventoryService _inventoryService;
    private readonly RequestService _requestService;
    private readonly RequestWorkflowService _requestWorkflow;
    private readonly LoanWorkflowService _loanWorkflow;
    private readonly PurchaseOrderWorkflowService _purchaseOrderWorkflow;
    private readonly InventoryAdjustmentWorkflowService _adjustmentWorkflow;
    private readonly SupplierService _supplierService;
    private readonly IHostEnvironment _environment;

    public DemoDataSeeder(
        InventoryDbContext dbContext,
        UserService userService,
        AssetService assetService,
        InventoryService inventoryService,
        RequestService requestService,
        RequestWorkflowService requestWorkflow,
        LoanWorkflowService loanWorkflow,
        PurchaseOrderWorkflowService purchaseOrderWorkflow,
        InventoryAdjustmentWorkflowService adjustmentWorkflow,
        SupplierService supplierService,
        IHostEnvironment environment)
    {
        _dbContext = dbContext;
        _userService = userService;
        _assetService = assetService;
        _inventoryService = inventoryService;
        _requestService = requestService;
        _requestWorkflow = requestWorkflow;
        _loanWorkflow = loanWorkflow;
        _purchaseOrderWorkflow = purchaseOrderWorkflow;
        _adjustmentWorkflow = adjustmentWorkflow;
        _supplierService = supplierService;
        _environment = environment;
    }

    public async Task SeedAsync(bool resetDatabase = true, CancellationToken cancellationToken = default)
    {
        if (!_environment.IsDevelopment())
        {
            throw new InvalidOperationException("Demo seeding allowed only in Development.");
        }

        if (resetDatabase)
        {
            await _dbContext.Database.EnsureDeletedAsync(cancellationToken);
            await _dbContext.Database.MigrateAsync(cancellationToken);
        }

        await EnsureSuperAdminAsync(cancellationToken);
    }

    private async Task<User> EnsureSuperAdminAsync(CancellationToken cancellationToken)
    {
        var existing = await _dbContext.Users.FirstOrDefaultAsync(u => u.Username == SuperAdminUsername, cancellationToken);
        User user;
        if (existing is null)
        {
            user = await _userService.CreateUserAsync(
                new CreateUserCommand(SuperAdminUsername, SuperAdminPassword, SuperAdminEmail),
                cancellationToken);
        }
        else
        {
            user = existing;
        }

        await _userService.AssignRoleAsync(user.Id, RoleNames.SuperAdmin, cancellationToken);
        return user;
    }

    private async Task<Supplier> EnsureSupplierAsync(
        Guid actorUserId,
        string name,
        CancellationToken cancellationToken)
    {
        var existing = await _dbContext.Suppliers.FirstOrDefaultAsync(s => s.Name == name, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        return await _supplierService.CreateAsync(
            actorUserId,
            name,
            null,
            null,
            null,
            null,
            cancellationToken);
    }

    private async Task<Asset> EnsureAssetAsync(
        string assetCode,
        AssetType type,
        CancellationToken cancellationToken)
    {
        var existing = await _dbContext.Assets.FirstOrDefaultAsync(a => a.AssetCode == assetCode, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        return await _assetService.CreateAssetAsync(
            new CreateAssetCommand(assetCode, type, null, AssetStatus.Active),
            cancellationToken);
    }

    private async Task<Dictionary<string, InventoryItem>> EnsureInventoryAsync(CancellationToken cancellationToken)
    {
        var items = new List<(string Name, string Unit, ItemType ItemType, decimal Quantity, decimal ReorderLevel, decimal AverageCost)>
        {
            ("Engine Oil", "L", ItemType.Consumable, 120m, 30m, 12.5m),
            ("Brake Fluid", "L", ItemType.Consumable, 60m, 15m, 8m),
            ("Tires", "pcs", ItemType.Consumable, 30m, 8m, 110m),
            ("Wrench Set", "set", ItemType.NonConsumable, 4m, 1m, 300m),
            ("Diagnostic Scanner", "pcs", ItemType.NonConsumable, 2m, 1m, 900m)
        };

        var inventoryMap = new Dictionary<string, InventoryItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            var existing = await _dbContext.InventoryItems.FirstOrDefaultAsync(i => i.Name == item.Name, cancellationToken);
            if (existing is null)
            {
                var created = await _inventoryService.CreateItemAsync(
                    new CreateInventoryItemCommand(
                        item.Name,
                        item.Unit,
                        item.ItemType,
                        0m,
                        item.ReorderLevel,
                        null,
                        null),
                    cancellationToken);
                inventoryMap[item.Name] = created;
            }
            else
            {
                inventoryMap[item.Name] = existing;
            }
        }

        return inventoryMap;
    }

    private async Task SeedBaselinePurchaseOrderAsync(
        Guid ioUserId,
        Guid managerId,
        Guid financeId,
        Guid ceoId,
        Guid supplierId,
        Dictionary<string, InventoryItem> inventory,
        CancellationToken cancellationToken)
    {
        if (await _dbContext.PurchaseOrders.AnyAsync(po => po.Notes == "DEMO_PO_BASELINE", cancellationToken))
        {
            return;
        }

        var lines = new List<PurchaseOrderLineInput>
        {
            new(inventory["Engine Oil"].Id, 120m, 12.5m, null),
            new(inventory["Brake Fluid"].Id, 60m, 8m, null),
            new(inventory["Tires"].Id, 30m, 110m, null),
            new(inventory["Wrench Set"].Id, 4m, 300m, null),
            new(inventory["Diagnostic Scanner"].Id, 2m, 900m, null)
        };

        var po = await _purchaseOrderWorkflow.CreateDraftAsync(
            new CreatePurchaseOrderDraftCommand(
                ioUserId,
                supplierId,
                "DEMO_PO_BASELINE",
                lines),
            cancellationToken);

        await _purchaseOrderWorkflow.SubmitAsync(po.Id, ioUserId, cancellationToken);
        await _purchaseOrderWorkflow.ApplyDecisionAsync(po.Id, managerId, ApprovalDecision.Approve, "Approved", cancellationToken);
        await _purchaseOrderWorkflow.ApplyDecisionAsync(po.Id, financeId, ApprovalDecision.Approve, "Approved", cancellationToken);
        await _purchaseOrderWorkflow.ApplyDecisionAsync(po.Id, ceoId, ApprovalDecision.Approve, "Approved", cancellationToken);

        var poLines = await _dbContext.PurchaseOrderLines
            .Where(line => line.PurchaseOrderId == po.Id)
            .ToListAsync(cancellationToken);

        await _purchaseOrderWorkflow.ReceiveAsync(
            po.Id,
            ioUserId,
            poLines.Select(line => new PurchaseOrderReceiveLineInput(line.Id, line.QtyOrdered, null)).ToList(),
            "Baseline receive",
            cancellationToken);
    }

    private async Task SeedPurchaseOrdersAsync(
        Guid ioUserId,
        Guid managerId,
        Guid financeId,
        Guid ceoId,
        Guid supplierAId,
        Guid supplierBId,
        Dictionary<string, InventoryItem> inventory,
        CancellationToken cancellationToken)
    {
        if (await _dbContext.PurchaseOrders.AnyAsync(po => po.Notes == "DEMO_PO_PARTIAL", cancellationToken))
        {
            return;
        }

        var po1 = await _purchaseOrderWorkflow.CreateDraftAsync(
            new CreatePurchaseOrderDraftCommand(
                ioUserId,
                supplierAId,
                "DEMO_PO_PARTIAL",
                new[]
                {
                    new PurchaseOrderLineInput(inventory["Engine Oil"].Id, 50m, 14m, null),
                    new PurchaseOrderLineInput(inventory["Brake Fluid"].Id, 30m, 9m, null),
                    new PurchaseOrderLineInput(inventory["Tires"].Id, 10m, 120m, null),
                    new PurchaseOrderLineInput(inventory["Wrench Set"].Id, 2m, 315m, null),
                    new PurchaseOrderLineInput(inventory["Diagnostic Scanner"].Id, 1m, 975m, null)
                }),
            cancellationToken);

        await _purchaseOrderWorkflow.SubmitAsync(po1.Id, ioUserId, cancellationToken);
        await _purchaseOrderWorkflow.ApplyDecisionAsync(po1.Id, managerId, ApprovalDecision.Approve, "Approved", cancellationToken);
        await _purchaseOrderWorkflow.ApplyDecisionAsync(po1.Id, financeId, ApprovalDecision.Approve, "Approved", cancellationToken);
        await _purchaseOrderWorkflow.ApplyDecisionAsync(po1.Id, ceoId, ApprovalDecision.Approve, "Approved", cancellationToken);

        var po1Lines = await _dbContext.PurchaseOrderLines
            .Where(line => line.PurchaseOrderId == po1.Id)
            .ToListAsync(cancellationToken);

        var partialReceiptLines = po1Lines
            .Where(line =>
                line.InventoryId == inventory["Engine Oil"].Id ||
                line.InventoryId == inventory["Brake Fluid"].Id ||
                line.InventoryId == inventory["Tires"].Id)
            .Select(line => new PurchaseOrderReceiveLineInput(
                line.Id,
                line.InventoryId == inventory["Engine Oil"].Id ? 25m :
                line.InventoryId == inventory["Brake Fluid"].Id ? 15m : 5m,
                null))
            .ToList();

        await _purchaseOrderWorkflow.ReceiveAsync(
            po1.Id,
            ioUserId,
            partialReceiptLines,
            "Partial receive (3 of 5 items)",
            cancellationToken);

        var po2 = await _purchaseOrderWorkflow.CreateDraftAsync(
            new CreatePurchaseOrderDraftCommand(
                ioUserId,
                supplierBId,
                "DEMO_PO_PENDING_CEO",
                new[]
                {
                    new PurchaseOrderLineInput(inventory["Wrench Set"].Id, 2m, 310m, null),
                    new PurchaseOrderLineInput(inventory["Diagnostic Scanner"].Id, 1m, 950m, null)
                }),
            cancellationToken);

        await _purchaseOrderWorkflow.SubmitAsync(po2.Id, ioUserId, cancellationToken);
        await _purchaseOrderWorkflow.ApplyDecisionAsync(po2.Id, managerId, ApprovalDecision.Approve, "Approved", cancellationToken);
        await _purchaseOrderWorkflow.ApplyDecisionAsync(po2.Id, financeId, ApprovalDecision.Approve, "Approved", cancellationToken);
    }

    private async Task SeedMaintenanceRequestsAsync(
        Guid requesterId,
        Guid ioUserId,
        Guid managerId,
        Guid assetId,
        Dictionary<string, InventoryItem> inventory,
        CancellationToken cancellationToken)
    {
        var hasClosed = await _dbContext.Requests.AnyAsync(r => r.Purpose == "DEMO_MAINT_CLOSED", cancellationToken);
        if (!hasClosed)
        {
            var submit = await _requestWorkflow.SubmitMaintenanceIssueAsync(
                new SubmitMaintenanceIssueCommand(
                    requesterId,
                    assetId,
                    "DEMO_MAINT_CLOSED",
                    new[]
                    {
                        new RequestLineInput(inventory["Engine Oil"].Id, 5m, null),
                        new RequestLineInput(inventory["Brake Fluid"].Id, 2m, null)
                    }),
                cancellationToken);

            var request = await _dbContext.Requests
                .Include(r => r.Lines)
                .FirstAsync(r => r.Id == submit.RequestId, cancellationToken);

            await _requestWorkflow.InventoryOfficerReviewAsync(
                request.Id,
                ioUserId,
                request.Lines.Select(line => (line.Id, line.QtyRequested, (string?)null)).ToList(),
                "Approved",
                cancellationToken);

            await _requestWorkflow.ManagerDecisionAsync(
                request.Id,
                managerId,
                ApprovalDecision.Approve,
                "Approved",
                cancellationToken);

            await _requestWorkflow.IssueRequest(request.Id, ioUserId, cancellationToken);
        }

        var hasPendingIo = await _dbContext.Requests.AnyAsync(r => r.Purpose == "DEMO_MAINT_PENDING_IO", cancellationToken);
        if (!hasPendingIo)
        {
            await _requestWorkflow.SubmitMaintenanceIssueAsync(
                new SubmitMaintenanceIssueCommand(
                    requesterId,
                    assetId,
                    "DEMO_MAINT_PENDING_IO",
                    new[]
                    {
                        new RequestLineInput(inventory["Brake Fluid"].Id, 5m, null)
                    }),
                cancellationToken);
        }
    }

    private async Task SeedBorrowRequestAsync(
        Guid requesterId,
        Guid ioUserId,
        Guid managerId,
        Guid assetId,
        Dictionary<string, InventoryItem> inventory,
        CancellationToken cancellationToken)
    {
        var hasBorrow = await _dbContext.Requests.AnyAsync(r => r.Purpose == "DEMO_BORROW", cancellationToken);
        if (hasBorrow)
        {
            return;
        }

        var draft = await _requestService.CreateRequestAsync(
            new CreateRequestCommand(
                RequestType.Borrow,
                requesterId,
                assetId,
                "DEMO_BORROW",
                new[]
                {
                    new RequestLineInput(inventory["Wrench Set"].Id, 2m, null),
                    new RequestLineInput(inventory["Diagnostic Scanner"].Id, 1m, null)
                }),
            RequestStatus.Draft,
            cancellationToken);

        await _requestWorkflow.SubmitRequest(draft.Id, cancellationToken);

        var request = await _dbContext.Requests
            .Include(r => r.Lines)
            .FirstAsync(r => r.Id == draft.Id, cancellationToken);

        await _requestWorkflow.InventoryOfficerReviewAsync(
            request.Id,
            ioUserId,
            request.Lines.Select(line => (line.Id, line.QtyRequested, (string?)null)).ToList(),
            "Approved",
            cancellationToken);

        await _requestWorkflow.ManagerDecisionAsync(
            request.Id,
            managerId,
            ApprovalDecision.Approve,
            "Approved",
            cancellationToken);

        await _requestWorkflow.IssueRequest(request.Id, ioUserId, cancellationToken);

        var loan = await _dbContext.Loans
            .Include(l => l.Lines)
            .FirstAsync(l => l.RequestId == request.Id, cancellationToken);

        var firstLine = loan.Lines.First();
        await _loanWorkflow.ReturnLoanAsync(
            loan.Id,
            ioUserId,
            new[]
            {
                new ReturnLoanLineInput(firstLine.Id, 1m, ReturnCondition.Good, null)
            },
            cancellationToken);
    }

    private async Task SeedAdjustmentsAsync(
        Guid ioUserId,
        Guid managerId,
        Dictionary<string, InventoryItem> inventory,
        CancellationToken cancellationToken)
    {
        if (!await _dbContext.InventoryAdjustments.AnyAsync(a => a.Reason == "DEMO_ADJUSTMENT_SHRINKAGE", cancellationToken))
        {
            var draft = await _adjustmentWorkflow.CreateDraftAsync(
                new CreateInventoryAdjustmentDraftCommand(
                    ioUserId,
                    "DEMO_ADJUSTMENT_SHRINKAGE",
                    new[]
                    {
                        new InventoryAdjustmentLineInput(inventory["Tires"].Id, -2m, "Shrinkage adjustment")
                    }),
                cancellationToken);

            await _adjustmentWorkflow.SubmitAsync(draft.AdjustmentId, ioUserId, cancellationToken);
            await _adjustmentWorkflow.ApproveAsync(draft.AdjustmentId, managerId, "Approved", cancellationToken);
        }

        if (!await _dbContext.InventoryAdjustments.AnyAsync(a => a.Reason == "DEMO_ADJUSTMENT_PENDING", cancellationToken))
        {
            var draft = await _adjustmentWorkflow.CreateDraftAsync(
                new CreateInventoryAdjustmentDraftCommand(
                    ioUserId,
                    "DEMO_ADJUSTMENT_PENDING",
                    new[]
                    {
                        new InventoryAdjustmentLineInput(inventory["Engine Oil"].Id, 3m, "Found stock")
                    }),
                cancellationToken);

            await _adjustmentWorkflow.SubmitAsync(draft.AdjustmentId, ioUserId, cancellationToken);
        }
    }
}
