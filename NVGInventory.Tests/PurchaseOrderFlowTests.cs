using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Entities;
using NVGInventory.Domain.Enums;
using NVGInventory.Domain.Exceptions;
using NVGInventory.Domain.Services;
using Xunit;
using Xunit.Abstractions;

namespace NVGInventory.Tests;

[Collection("SqlServerIntegration")]
public sealed class PurchaseOrderFlowTests : SqlServerIntegrationTestBase
{
    private readonly ITestOutputHelper _output;

    public PurchaseOrderFlowTests(SqlServerIntegrationFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _output = output;
    }

    [SqlServerFact]
    public async Task DraftPo_CreatedByInventoryOfficer()
    {
        await using var context = CreateDbContext();
        var supplier = await AddSupplierAsync(context, "Supplier A");
        var inventory = await AddInventoryItemAsync(context, "PO Item", 0m);
        var ioUser = await AddUserAsync(context, "po_io", RoleNames.InventoryOfficer);
        await context.SaveChangesAsync();

        var workflowService = CreateWorkflowService(context);
        var po = await workflowService.CreateDraftAsync(
            new CreatePurchaseOrderDraftCommand(
                ioUser.Id,
                supplier.Id,
                "Draft PO",
                new[] { new PurchaseOrderLineInput(inventory.Id, 5m, 100m, null) }));

        Assert.Equal(PurchaseOrderStatus.Draft, po.Status);
        Assert.Single(po.Lines);
    }

    [SqlServerFact]
    public async Task NonIo_CannotDraft()
    {
        await using var context = CreateDbContext();
        var supplier = await AddSupplierAsync(context, "Supplier B");
        var inventory = await AddInventoryItemAsync(context, "PO Item 2", 0m);
        var manager = await AddUserAsync(context, "po_manager", RoleNames.Manager);
        await context.SaveChangesAsync();

        var workflowService = CreateWorkflowService(context);
        await Assert.ThrowsAnyAsync<DomainException>(async () =>
        {
            await workflowService.CreateDraftAsync(
                new CreatePurchaseOrderDraftCommand(
                    manager.Id,
                    supplier.Id,
                    null,
                    new[] { new PurchaseOrderLineInput(inventory.Id, 3m, 100m, null) }));
        });
    }

    [SqlServerFact]
    public async Task FullApprovalChainRequired()
    {
        await using var context = CreateDbContext();
        var supplier = await AddSupplierAsync(context, "Supplier C");
        var inventory = await AddInventoryItemAsync(context, "PO Item 3", 0m);
        var ioUser = await AddUserAsync(context, "po_io_chain", RoleNames.InventoryOfficer);
        var manager = await AddUserAsync(context, "po_mgr_chain", RoleNames.Manager);
        var finance = await AddUserAsync(context, "po_fin_chain", RoleNames.HeadOfFinance);
        var ceo = await AddUserAsync(context, "po_ceo_chain", RoleNames.Ceo);
        await context.SaveChangesAsync();

        var workflowService = CreateWorkflowService(context);
        var po = await workflowService.CreateDraftAsync(
            new CreatePurchaseOrderDraftCommand(
                ioUser.Id,
                supplier.Id,
                null,
                new[] { new PurchaseOrderLineInput(inventory.Id, 4m, 100m, null) }));

        var submit = await workflowService.SubmitAsync(po.Id, ioUser.Id);
        Assert.Equal(PurchaseOrderStatus.PendingManager, submit.Status);

        var mgrDecision = await workflowService.ApplyDecisionAsync(
            po.Id,
            manager.Id,
            ApprovalDecision.Approve,
            null);
        Assert.Equal(PurchaseOrderStatus.PendingFinance, mgrDecision.Status);

        var finDecision = await workflowService.ApplyDecisionAsync(
            po.Id,
            finance.Id,
            ApprovalDecision.Approve,
            null);
        Assert.Equal(PurchaseOrderStatus.PendingCeo, finDecision.Status);

        var ceoDecision = await workflowService.ApplyDecisionAsync(
            po.Id,
            ceo.Id,
            ApprovalDecision.Approve,
            null);
        Assert.Equal(PurchaseOrderStatus.Approved, ceoDecision.Status);
    }

    [SqlServerFact]
    public async Task CannotReceiveBeforeApproved()
    {
        await using var context = CreateDbContext();
        var supplier = await AddSupplierAsync(context, "Supplier D");
        var inventory = await AddInventoryItemAsync(context, "PO Item 4", 0m);
        var ioUser = await AddUserAsync(context, "po_io_receive", RoleNames.InventoryOfficer);
        var manager = await AddUserAsync(context, "po_mgr_receive", RoleNames.Manager);
        await context.SaveChangesAsync();

        var workflowService = CreateWorkflowService(context);
        var po = await workflowService.CreateDraftAsync(
            new CreatePurchaseOrderDraftCommand(
                ioUser.Id,
                supplier.Id,
                null,
                new[] { new PurchaseOrderLineInput(inventory.Id, 5m, 100m, null) }));

        await workflowService.SubmitAsync(po.Id, ioUser.Id);

        await Assert.ThrowsAnyAsync<DomainException>(async () =>
        {
            await workflowService.ReceiveAsync(
                po.Id,
                ioUser.Id,
                new[] { new PurchaseOrderReceiveLineInput(po.Lines.Single().Id, 2m, null) },
                null);
        });

        var mgrDecision = await workflowService.ApplyDecisionAsync(
            po.Id,
            manager.Id,
            ApprovalDecision.Approve,
            null);
        Assert.Equal(PurchaseOrderStatus.PendingFinance, mgrDecision.Status);
    }

    [SqlServerFact]
    public async Task PartialReceipt_IncreasesStock_AndCreatesLogs()
    {
        await using var context = CreateDbContext();
        var supplier = await AddSupplierAsync(context, "Supplier E");
        var inventory = await AddInventoryItemAsync(context, "PO Item 5", 2m);
        var ioUser = await AddUserAsync(context, "po_io_partial", RoleNames.InventoryOfficer);
        var manager = await AddUserAsync(context, "po_mgr_partial", RoleNames.Manager);
        var finance = await AddUserAsync(context, "po_fin_partial", RoleNames.HeadOfFinance);
        var ceo = await AddUserAsync(context, "po_ceo_partial", RoleNames.Ceo);
        await context.SaveChangesAsync();

        var workflowService = CreateWorkflowService(context);
        var po = await workflowService.CreateDraftAsync(
            new CreatePurchaseOrderDraftCommand(
                ioUser.Id,
                supplier.Id,
                null,
                new[] { new PurchaseOrderLineInput(inventory.Id, 5m, 100m, null) }));

        await workflowService.SubmitAsync(po.Id, ioUser.Id);
        await workflowService.ApplyDecisionAsync(po.Id, manager.Id, ApprovalDecision.Approve, null);
        await workflowService.ApplyDecisionAsync(po.Id, finance.Id, ApprovalDecision.Approve, null);
        await workflowService.ApplyDecisionAsync(po.Id, ceo.Id, ApprovalDecision.Approve, null);

        var receive = await workflowService.ReceiveAsync(
            po.Id,
            ioUser.Id,
            new[] { new PurchaseOrderReceiveLineInput(po.Lines.Single().Id, 2m, null) },
            "partial");

        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, receive.Status);

        var updatedInventory = await context.InventoryItems.SingleAsync(i => i.Id == inventory.Id);
        Assert.Equal(4m, updatedInventory.Quantity);

        var logs = await context.StockLogs
            .Where(log => log.RefType == EntityTypes.PurchaseOrder && log.RefId == po.Id)
            .ToListAsync();

        Assert.Single(logs);
        Assert.Equal(StockMovementType.In, logs[0].MovementType);
        Assert.Equal(2m, logs[0].QtyDelta);
    }

    [SqlServerFact]
    public async Task OverReceipt_Throws()
    {
        await using var context = CreateDbContext();
        var supplier = await AddSupplierAsync(context, "Supplier F");
        var inventory = await AddInventoryItemAsync(context, "PO Item 6", 0m);
        var ioUser = await AddUserAsync(context, "po_io_over", RoleNames.InventoryOfficer);
        var manager = await AddUserAsync(context, "po_mgr_over", RoleNames.Manager);
        var finance = await AddUserAsync(context, "po_fin_over", RoleNames.HeadOfFinance);
        var ceo = await AddUserAsync(context, "po_ceo_over", RoleNames.Ceo);
        await context.SaveChangesAsync();

        var workflowService = CreateWorkflowService(context);
        var po = await workflowService.CreateDraftAsync(
            new CreatePurchaseOrderDraftCommand(
                ioUser.Id,
                supplier.Id,
                null,
                new[] { new PurchaseOrderLineInput(inventory.Id, 4m, 100m, null) }));

        await workflowService.SubmitAsync(po.Id, ioUser.Id);
        await workflowService.ApplyDecisionAsync(po.Id, manager.Id, ApprovalDecision.Approve, null);
        await workflowService.ApplyDecisionAsync(po.Id, finance.Id, ApprovalDecision.Approve, null);
        await workflowService.ApplyDecisionAsync(po.Id, ceo.Id, ApprovalDecision.Approve, null);

        await Assert.ThrowsAnyAsync<DomainException>(async () =>
        {
            await workflowService.ReceiveAsync(
                po.Id,
                ioUser.Id,
                new[] { new PurchaseOrderReceiveLineInput(po.Lines.Single().Id, 10m, null) },
                null);
        });
    }

    [SqlServerFact]
    public async Task Receive_RequiresUnitPrice()
    {
        await using var context = CreateDbContext();
        var supplier = await AddSupplierAsync(context, "Supplier Price");
        var inventory = await AddInventoryItemAsync(context, "PO Item Price", 0m);
        var ioUser = await AddUserAsync(context, "po_io_price", RoleNames.InventoryOfficer);
        var manager = await AddUserAsync(context, "po_mgr_price", RoleNames.Manager);
        var finance = await AddUserAsync(context, "po_fin_price", RoleNames.HeadOfFinance);
        var ceo = await AddUserAsync(context, "po_ceo_price", RoleNames.Ceo);
        await context.SaveChangesAsync();

        var workflowService = CreateWorkflowService(context);
        var po = await workflowService.CreateDraftAsync(
            new CreatePurchaseOrderDraftCommand(
                ioUser.Id,
                supplier.Id,
                null,
                new[] { new PurchaseOrderLineInput(inventory.Id, 4m, null, null) }));

        await workflowService.SubmitAsync(po.Id, ioUser.Id);
        await workflowService.ApplyDecisionAsync(po.Id, manager.Id, ApprovalDecision.Approve, null);
        await workflowService.ApplyDecisionAsync(po.Id, finance.Id, ApprovalDecision.Approve, null);
        await workflowService.ApplyDecisionAsync(po.Id, ceo.Id, ApprovalDecision.Approve, null);

        await Assert.ThrowsAnyAsync<DomainException>(async () =>
        {
            await workflowService.ReceiveAsync(
                po.Id,
                ioUser.Id,
                new[] { new PurchaseOrderReceiveLineInput(po.Lines.Single().Id, 2m, null) },
                null);
        });
    }

    [SqlServerFact]
    public async Task FullyReceived_ClosesOrder()
    {
        await using var context = CreateDbContext();
        var supplier = await AddSupplierAsync(context, "Supplier G");
        var inventory = await AddInventoryItemAsync(context, "PO Item 7", 1m);
        var ioUser = await AddUserAsync(context, "po_io_close", RoleNames.InventoryOfficer);
        var manager = await AddUserAsync(context, "po_mgr_close", RoleNames.Manager);
        var finance = await AddUserAsync(context, "po_fin_close", RoleNames.HeadOfFinance);
        var ceo = await AddUserAsync(context, "po_ceo_close", RoleNames.Ceo);
        await context.SaveChangesAsync();

        var workflowService = CreateWorkflowService(context);
        var po = await workflowService.CreateDraftAsync(
            new CreatePurchaseOrderDraftCommand(
                ioUser.Id,
                supplier.Id,
                null,
                new[] { new PurchaseOrderLineInput(inventory.Id, 2m, 100m, null) }));

        await workflowService.SubmitAsync(po.Id, ioUser.Id);
        await workflowService.ApplyDecisionAsync(po.Id, manager.Id, ApprovalDecision.Approve, null);
        await workflowService.ApplyDecisionAsync(po.Id, finance.Id, ApprovalDecision.Approve, null);
        await workflowService.ApplyDecisionAsync(po.Id, ceo.Id, ApprovalDecision.Approve, null);

        var receive = await workflowService.ReceiveAsync(
            po.Id,
            ioUser.Id,
            new[] { new PurchaseOrderReceiveLineInput(po.Lines.Single().Id, 2m, null) },
            null);

        Assert.Equal(PurchaseOrderStatus.Closed, receive.Status);
    }

    [SqlServerFact]
    public async Task DoubleReceiveBeyondRemaining_Fails()
    {
        await using var context = CreateDbContext();
        var supplier = await AddSupplierAsync(context, "Supplier H");
        var inventory = await AddInventoryItemAsync(context, "PO Item 8", 0m);
        var ioUser = await AddUserAsync(context, "po_io_double", RoleNames.InventoryOfficer);
        var manager = await AddUserAsync(context, "po_mgr_double", RoleNames.Manager);
        var finance = await AddUserAsync(context, "po_fin_double", RoleNames.HeadOfFinance);
        var ceo = await AddUserAsync(context, "po_ceo_double", RoleNames.Ceo);
        await context.SaveChangesAsync();

        var workflowService = CreateWorkflowService(context);
        var po = await workflowService.CreateDraftAsync(
            new CreatePurchaseOrderDraftCommand(
                ioUser.Id,
                supplier.Id,
                null,
                new[] { new PurchaseOrderLineInput(inventory.Id, 5m, 100m, null) }));

        await workflowService.SubmitAsync(po.Id, ioUser.Id);
        await workflowService.ApplyDecisionAsync(po.Id, manager.Id, ApprovalDecision.Approve, null);
        await workflowService.ApplyDecisionAsync(po.Id, finance.Id, ApprovalDecision.Approve, null);
        await workflowService.ApplyDecisionAsync(po.Id, ceo.Id, ApprovalDecision.Approve, null);

        await workflowService.ReceiveAsync(
            po.Id,
            ioUser.Id,
            new[] { new PurchaseOrderReceiveLineInput(po.Lines.Single().Id, 3m, null) },
            null);

        await Assert.ThrowsAnyAsync<DomainException>(async () =>
        {
            await workflowService.ReceiveAsync(
                po.Id,
                ioUser.Id,
                new[] { new PurchaseOrderReceiveLineInput(po.Lines.Single().Id, 3m, null) },
                null);
        });
    }

    [SqlServerFact]
    public async Task InventoryAverageCost_UpdatesOnPoReceives()
    {
        await using var context = CreateDbContext();
        var supplier = await AddSupplierAsync(context, "Supplier Cost");
        var inventory = await AddInventoryItemAsync(context, "Cost Item", 10m, 100m);
        var ioUser = await AddUserAsync(context, "po_io_cost", RoleNames.InventoryOfficer);
        var manager = await AddUserAsync(context, "po_mgr_cost", RoleNames.Manager);
        var finance = await AddUserAsync(context, "po_fin_cost", RoleNames.HeadOfFinance);
        var ceo = await AddUserAsync(context, "po_ceo_cost", RoleNames.Ceo);
        await context.SaveChangesAsync();

        var workflowService = CreateWorkflowService(context);

        var po1 = await workflowService.CreateDraftAsync(
            new CreatePurchaseOrderDraftCommand(
                ioUser.Id,
                supplier.Id,
                null,
                new[] { new PurchaseOrderLineInput(inventory.Id, 5m, 200m, null) }));

        await workflowService.SubmitAsync(po1.Id, ioUser.Id);
        await workflowService.ApplyDecisionAsync(po1.Id, manager.Id, ApprovalDecision.Approve, null);
        await workflowService.ApplyDecisionAsync(po1.Id, finance.Id, ApprovalDecision.Approve, null);
        await workflowService.ApplyDecisionAsync(po1.Id, ceo.Id, ApprovalDecision.Approve, null);

        await workflowService.ReceiveAsync(
            po1.Id,
            ioUser.Id,
            new[] { new PurchaseOrderReceiveLineInput(po1.Lines.Single().Id, 5m, null) },
            null);

        context.ChangeTracker.Clear();
        var afterFirst = await context.InventoryItems.AsNoTracking().SingleAsync(item => item.Id == inventory.Id);
        var expectedFirstAvg = Math.Round(((10m * 100m) + (5m * 200m)) / 15m, 4, MidpointRounding.AwayFromZero);
        Assert.Equal(15m, afterFirst.Quantity);
        Assert.Equal(expectedFirstAvg, afterFirst.AverageCost);
        _output.WriteLine(
            "After first receive: qty={0}, avgCost={1}",
            afterFirst.Quantity,
            afterFirst.AverageCost);

        var firstLogs = await context.StockLogs
            .Where(log => log.RefType == EntityTypes.PurchaseOrder && log.RefId == po1.Id)
            .ToListAsync();
        Assert.Single(firstLogs);
        Assert.Equal(StockMovementType.In, firstLogs[0].MovementType);

        var po2 = await workflowService.CreateDraftAsync(
            new CreatePurchaseOrderDraftCommand(
                ioUser.Id,
                supplier.Id,
                null,
                new[] { new PurchaseOrderLineInput(inventory.Id, 5m, 300m, null) }));

        await workflowService.SubmitAsync(po2.Id, ioUser.Id);
        await workflowService.ApplyDecisionAsync(po2.Id, manager.Id, ApprovalDecision.Approve, null);
        await workflowService.ApplyDecisionAsync(po2.Id, finance.Id, ApprovalDecision.Approve, null);
        await workflowService.ApplyDecisionAsync(po2.Id, ceo.Id, ApprovalDecision.Approve, null);

        await workflowService.ReceiveAsync(
            po2.Id,
            ioUser.Id,
            new[] { new PurchaseOrderReceiveLineInput(po2.Lines.Single().Id, 5m, null) },
            null);

        context.ChangeTracker.Clear();
        var afterSecond = await context.InventoryItems.AsNoTracking().SingleAsync(item => item.Id == inventory.Id);
        var expectedSecondAvg = Math.Round(((15m * expectedFirstAvg) + (5m * 300m)) / 20m, 4, MidpointRounding.AwayFromZero);
        Assert.Equal(20m, afterSecond.Quantity);
        Assert.Equal(expectedSecondAvg, afterSecond.AverageCost);
        _output.WriteLine(
            "After second receive: qty={0}, avgCost={1}",
            afterSecond.Quantity,
            afterSecond.AverageCost);
    }

    [SqlServerFact]
    public async Task DraftPo_RejectsInactiveInventory()
    {
        await using var context = CreateDbContext();
        var supplier = await AddSupplierAsync(context, "Supplier Inactive Item");
        var ioUser = await AddUserAsync(context, "po_io_inactive_item", RoleNames.InventoryOfficer);

        var inactiveItem = new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = "Inactive Item",
            Unit = "PCS",
            ItemType = ItemType.Consumable,
            Quantity = 0m,
            IsActive = false,
            CreatedAt = DateTime.UtcNow
        };
        context.InventoryItems.Add(inactiveItem);
        await context.SaveChangesAsync();

        var workflowService = CreateWorkflowService(context);
        await Assert.ThrowsAnyAsync<DomainException>(async () =>
        {
            await workflowService.CreateDraftAsync(
                new CreatePurchaseOrderDraftCommand(
                    ioUser.Id,
                    supplier.Id,
                    null,
                    new[] { new PurchaseOrderLineInput(inactiveItem.Id, 3m, 100m, null) }));
        });
    }

    private static PurchaseOrderWorkflowService CreateWorkflowService(InventoryDbContext context)
    {
        var userService = new UserService(context);
        var approvalService = new ApprovalService(context, userService);
        var stockLedger = new StockLedgerService(context);
        var poService = new PurchaseOrderService(context);

        return new PurchaseOrderWorkflowService(
            context,
            poService,
            approvalService,
            stockLedger,
            userService);
    }

    private static async Task<User> AddUserAsync(InventoryDbContext context, string username, params string[] roles)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            PasswordHash = TestPasswords.Hashed,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Users.Add(user);

        if (roles.Length > 0)
        {
            var roleEntities = await context.Roles
                .Where(role => roles.Contains(role.Name))
                .ToListAsync();

            foreach (var role in roleEntities)
            {
                context.UserRoles.Add(new UserRole
                {
                    UserId = user.Id,
                    RoleId = role.Id
                });
            }
        }

        return user;
    }

    private static Task<Supplier> AddSupplierAsync(InventoryDbContext context, string name)
    {
        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            Name = name,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Suppliers.Add(supplier);
        return Task.FromResult(supplier);
    }

    private static Task<InventoryItem> AddInventoryItemAsync(
        InventoryDbContext context,
        string name,
        decimal quantity,
        decimal averageCost = 0m)
    {
        var item = new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = name,
            Unit = "PCS",
            ItemType = ItemType.Consumable,
            Quantity = quantity,
            AverageCost = averageCost,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.InventoryItems.Add(item);
        return Task.FromResult(item);
    }
}
