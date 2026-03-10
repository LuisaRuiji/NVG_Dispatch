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

namespace NVGInventory.Tests;

[Collection("SqlServerIntegration")]
public sealed class InventoryAdjustmentFlowTests : SqlServerIntegrationTestBase
{
    public InventoryAdjustmentFlowTests(SqlServerIntegrationFixture fixture) : base(fixture)
    {
    }

    [SqlServerFact]
    public async Task Adjustment_IssueRequiresManagerApproval()
    {
        await using var context = CreateDbContext();
        var (ioUser, manager, item) = await SeedAdjustmentActorsAsync(context, 5m);
        await context.SaveChangesAsync();

        var workflowService = CreateWorkflowService(context);
        var submit = await workflowService.SubmitAdjustmentAsync(
            new SubmitAdjustmentCommand(
                ioUser.Id,
                "Damaged stock",
                new[] { new RequestLineInput(item.Id, -2m, null) }));

        var request = await context.Requests.Include(r => r.Lines).SingleAsync(r => r.Id == submit.RequestId);
        await workflowService.InventoryOfficerReviewAsync(
            request.Id,
            ioUser.Id,
            new[] { (request.Lines.Single().Id, -2m, (string?)null) },
            "Reviewed");

        await Assert.ThrowsAnyAsync<DomainException>(async () =>
        {
            await workflowService.IssueRequest(request.Id, ioUser.Id);
        });
    }

    [SqlServerFact]
    public async Task Adjustment_IssueCreatesAdjustmentLog_AndCloses()
    {
        await using var context = CreateDbContext();
        var (ioUser, manager, item) = await SeedAdjustmentActorsAsync(context, 5m);
        await context.SaveChangesAsync();

        var workflowService = CreateWorkflowService(context);
        var submit = await workflowService.SubmitAdjustmentAsync(
            new SubmitAdjustmentCommand(
                ioUser.Id,
                "Damaged stock",
                new[] { new RequestLineInput(item.Id, -2m, null) }));

        var request = await context.Requests.Include(r => r.Lines).SingleAsync(r => r.Id == submit.RequestId);
        await workflowService.InventoryOfficerReviewAsync(
            request.Id,
            ioUser.Id,
            new[] { (request.Lines.Single().Id, -2m, (string?)null) },
            "Reviewed");

        await workflowService.ManagerDecisionAsync(request.Id, manager.Id, ApprovalDecision.Approve, "Approved");
        await workflowService.IssueRequest(request.Id, ioUser.Id);

        var updatedRequest = await context.Requests.SingleAsync(r => r.Id == request.Id);
        Assert.Equal(RequestStatus.Closed, updatedRequest.Status);

        var updatedItem = await context.InventoryItems.SingleAsync(i => i.Id == item.Id);
        Assert.Equal(3m, updatedItem.Quantity);

        var log = await context.StockLogs
            .SingleAsync(entry => entry.RefType == EntityTypes.Request && entry.RefId == request.Id);

        Assert.Equal(StockMovementType.Adjustment, log.MovementType);
        Assert.Equal(-2m, log.QtyDelta);
    }

    private static RequestWorkflowService CreateWorkflowService(InventoryDbContext context)
    {
        var userService = new UserService(context);
        var approvalService = new ApprovalService(context, userService);
        var requestService = new RequestService(context);
        var stockLedger = new StockLedgerService(context);

        return new RequestWorkflowService(
            context,
            requestService,
            approvalService,
            stockLedger,
            userService);
    }

    private static async Task<(User IoUser, User Manager, InventoryItem Item)> SeedAdjustmentActorsAsync(
        InventoryDbContext context,
        decimal quantity)
    {
        var ioUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "adj_io",
            PasswordHash = TestPasswords.Hashed,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var manager = new User
        {
            Id = Guid.NewGuid(),
            Username = "adj_mgr",
            PasswordHash = TestPasswords.Hashed,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Users.AddRange(ioUser, manager);

        var ioRole = await context.Roles.SingleAsync(role => role.Name == RoleNames.InventoryOfficer);
        var managerRole = await context.Roles.SingleAsync(role => role.Name == RoleNames.Manager);

        context.UserRoles.AddRange(
            new UserRole { UserId = ioUser.Id, RoleId = ioRole.Id },
            new UserRole { UserId = manager.Id, RoleId = managerRole.Id });

        var item = new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = "Adjustment Item",
            Unit = "PCS",
            ItemType = ItemType.Consumable,
            Quantity = quantity,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.InventoryItems.Add(item);
        return (ioUser, manager, item);
    }
}
