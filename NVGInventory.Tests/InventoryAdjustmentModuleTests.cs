using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Entities;
using NVGInventory.Domain.Enums;
using NVGInventory.Domain.Services;
using Xunit;

namespace NVGInventory.Tests;

[Collection("SqlServerIntegration")]
public sealed class InventoryAdjustmentModuleTests : SqlServerIntegrationTestBase
{
    public InventoryAdjustmentModuleTests(SqlServerIntegrationFixture fixture) : base(fixture)
    {
    }

    [SqlServerFact]
    public async Task InventoryAdjustment_AppliesSignedDeltas_AndPreservesAverageCost()
    {
        Guid ioUserId;
        Guid managerUserId;
        Guid inventoryId;

        await using (var setupContext = CreateDbContext())
        {
            var (ioUser, manager, item) = await SeedAdjustmentActorsAsync(setupContext, 10m, 100m);
            await setupContext.SaveChangesAsync();

            ioUserId = ioUser.Id;
            managerUserId = manager.Id;
            inventoryId = item.Id;
        }

        await using (var context = CreateDbContext())
        {
            var workflowService = CreateWorkflowService(context);

            var draft = await workflowService.CreateDraftAsync(
                new CreateInventoryAdjustmentDraftCommand(
                    ioUserId,
                    "Shrinkage",
                    new[] { new InventoryAdjustmentLineInput(inventoryId, -3m, null) }));

            await workflowService.SubmitAsync(draft.AdjustmentId, ioUserId);
            await workflowService.ApproveAsync(draft.AdjustmentId, managerUserId, "Approved");
        }

        await using (var verifyContext = CreateDbContext())
        {
            var item = await verifyContext.InventoryItems.SingleAsync(i => i.Id == inventoryId);
            Assert.Equal(7m, item.Quantity);
            Assert.Equal(100m, item.AverageCost);

            var log = await verifyContext.StockLogs
                .SingleAsync(entry => entry.RefType == EntityTypes.InventoryAdjustment
                                      && entry.RefId == verifyContext.InventoryAdjustments
                                          .Select(adj => adj.Id)
                                          .First());

            Assert.Equal(300m, log.TotalCostSnapshot);
        }

        await using (var context = CreateDbContext())
        {
            var workflowService = CreateWorkflowService(context);

            var draft = await workflowService.CreateDraftAsync(
                new CreateInventoryAdjustmentDraftCommand(
                    ioUserId,
                    "Found stock",
                    new[] { new InventoryAdjustmentLineInput(inventoryId, 2m, null) }));

            await workflowService.SubmitAsync(draft.AdjustmentId, ioUserId);
            await workflowService.ApproveAsync(draft.AdjustmentId, managerUserId, "Approved");
        }

        await using (var verifyContext = CreateDbContext())
        {
            var item = await verifyContext.InventoryItems.SingleAsync(i => i.Id == inventoryId);
            Assert.Equal(9m, item.Quantity);
            Assert.Equal(100m, item.AverageCost);

            var logs = await verifyContext.StockLogs
                .Where(entry => entry.RefType == EntityTypes.InventoryAdjustment && entry.InventoryId == inventoryId)
                .ToListAsync();

            Assert.Equal(2, logs.Count);
            Assert.Contains(logs, log => log.TotalCostSnapshot == 300m);
            Assert.Contains(logs, log => log.TotalCostSnapshot == 200m);
        }
    }

    private static InventoryAdjustmentWorkflowService CreateWorkflowService(InventoryDbContext context)
    {
        var userService = new UserService(context);
        var approvalService = new ApprovalService(context, userService);
        var stockLedger = new StockLedgerService(context);
        return new InventoryAdjustmentWorkflowService(context, approvalService, stockLedger, userService);
    }

    private static async Task<(User IoUser, User Manager, InventoryItem Item)> SeedAdjustmentActorsAsync(
        InventoryDbContext context,
        decimal quantity,
        decimal averageCost)
    {
        var ioUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "adj_mod_io",
            PasswordHash = TestPasswords.Hashed,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var manager = new User
        {
            Id = Guid.NewGuid(),
            Username = "adj_mod_mgr",
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
            Name = "Adjustment Module Item",
            Unit = "PCS",
            ItemType = ItemType.Consumable,
            Quantity = quantity,
            AverageCost = averageCost,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.InventoryItems.Add(item);
        return (ioUser, manager, item);
    }
}
