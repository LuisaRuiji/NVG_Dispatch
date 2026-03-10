using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Entities;
using NVGInventory.Domain.Enums;
using NVGInventory.Domain.Exceptions;
using NVGInventory.Domain.Services;
using Xunit;

namespace NVGInventory.Tests;

[Collection("SqlServerIntegration")]
public class StockLedgerServiceTests : SqlServerIntegrationTestBase
{
    public StockLedgerServiceTests(SqlServerIntegrationFixture fixture) : base(fixture)
    {
    }

    [SqlServerFact]
    public async Task ApplyAsync_RollsBackWhenCommitHookFails()
    {
        Guid inventoryId;
        Guid actorId;

        await using (var setupContext = CreateDbContext())
        {
            var actor = new User
            {
                Id = Guid.NewGuid(),
                Username = "actor",
                PasswordHash = TestPasswords.Hashed
            };
            setupContext.Users.Add(actor);

            var item = new InventoryItem
            {
                Id = Guid.NewGuid(),
                Name = "Hydraulic Fluid",
                Unit = "L",
                ItemType = ItemType.Consumable,
                Quantity = 10m
            };
            setupContext.InventoryItems.Add(item);

            await setupContext.SaveChangesAsync();

            actorId = actor.Id;
            inventoryId = item.Id;
        }

        await using (var context = CreateDbContext())
        {
            var service = new StockLedgerService(context, () => throw new InvalidOperationException("boom"));

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await service.ApplyMovement(
                    StockMovementType.Out,
                    inventoryId,
                    -2m,
                    "request",
                    Guid.NewGuid(),
                    actorId);
            });
        }

        await using (var verifyContext = CreateDbContext())
        {
            var inventory = await verifyContext.InventoryItems.SingleAsync(i => i.Id == inventoryId);
            var logCount = await verifyContext.StockLogs.CountAsync(log => log.InventoryId == inventoryId);

            Assert.Equal(10m, inventory.Quantity);
            Assert.Equal(0, logCount);
        }
    }

    [SqlServerFact]
    public async Task ApplyAsync_RejectsNegativeStock()
    {
        Guid inventoryId;
        Guid actorId;

        await using (var setupContext = CreateDbContext())
        {
            var actor = new User
            {
                Id = Guid.NewGuid(),
                Username = "actor2",
                PasswordHash = TestPasswords.Hashed
            };
            setupContext.Users.Add(actor);

            var item = new InventoryItem
            {
                Id = Guid.NewGuid(),
                Name = "Brake Fluid",
                Unit = "L",
                ItemType = ItemType.Consumable,
                Quantity = 3m
            };
            setupContext.InventoryItems.Add(item);

            await setupContext.SaveChangesAsync();

            actorId = actor.Id;
            inventoryId = item.Id;
        }

        await using (var context = CreateDbContext())
        {
            var service = new StockLedgerService(context);

            await Assert.ThrowsAnyAsync<DomainException>(async () =>
            {
                await service.ApplyMovement(
                    StockMovementType.Out,
                    inventoryId,
                    -10m,
                    "request",
                    Guid.NewGuid(),
                    actorId);
            });
        }

        await using (var verifyContext = CreateDbContext())
        {
            var inventory = await verifyContext.InventoryItems.SingleAsync(i => i.Id == inventoryId);
            var logCount = await verifyContext.StockLogs.CountAsync(log => log.InventoryId == inventoryId);

            Assert.Equal(3m, inventory.Quantity);
            Assert.Equal(0, logCount);
        }
    }

    [SqlServerFact]
    public async Task Adjustment_DoesNotChangeAverageCost()
    {
        Guid inventoryId;
        Guid actorId;

        await using (var setupContext = CreateDbContext())
        {
            var actor = new User
            {
                Id = Guid.NewGuid(),
                Username = "actor3",
                PasswordHash = TestPasswords.Hashed
            };
            setupContext.Users.Add(actor);

            var item = new InventoryItem
            {
                Id = Guid.NewGuid(),
                Name = "Filter Oil",
                Unit = "PCS",
                ItemType = ItemType.Consumable,
                Quantity = 10m,
                AverageCost = 100m
            };
            setupContext.InventoryItems.Add(item);

            await setupContext.SaveChangesAsync();

            actorId = actor.Id;
            inventoryId = item.Id;
        }

        await using (var context = CreateDbContext())
        {
            var service = new StockLedgerService(context);

            await service.ApplyMovement(
                StockMovementType.Adjustment,
                inventoryId,
                -2m,
                "adjustment",
                Guid.NewGuid(),
                actorId);
        }

        await using (var verifyContext = CreateDbContext())
        {
            var inventory = await verifyContext.InventoryItems.SingleAsync(i => i.Id == inventoryId);
            var log = await verifyContext.StockLogs.SingleAsync(entry => entry.InventoryId == inventoryId);
            Assert.Equal(8m, inventory.Quantity);
            Assert.Equal(100m, inventory.AverageCost);
            Assert.Equal(100m, log.UnitCostSnapshot);
            Assert.Equal(200m, log.TotalCostSnapshot);
        }
    }
}
