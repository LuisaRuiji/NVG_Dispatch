using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Entities;
using NVGInventory.Domain.Enums;
using NVGInventory.Domain.Exceptions;
using NVGInventory.Domain.Services;
using Xunit;

namespace NVGInventory.Tests;

public class RequestServiceTests
{
    [Fact]
    public async Task MaintenanceIssue_RequiresAsset()
    {
        await using var dbContext = CreateDbContext();
        var requestService = new RequestService(dbContext);

        var requesterId = Guid.NewGuid();
        dbContext.Users.Add(new User
        {
            Id = requesterId,
            Username = "tester",
            PasswordHash = TestPasswords.Hashed
        });

        var inventoryItem = new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = "Oil",
            Unit = "L",
            ItemType = ItemType.Consumable
        };
        dbContext.InventoryItems.Add(inventoryItem);
        await dbContext.SaveChangesAsync();

        var command = new CreateRequestCommand(
            RequestType.MaintenanceIssue,
            requesterId,
            null,
            "Routine service",
            new[] { new RequestLineInput(inventoryItem.Id, 2, null) });

        await Assert.ThrowsAnyAsync<DomainException>(() => requestService.CreateRequestAsync(command));
    }

    [Fact]
    public async Task MaintenanceIssue_RejectsNonConsumableItems()
    {
        await using var dbContext = CreateDbContext();
        var requestService = new RequestService(dbContext);

        var requesterId = Guid.NewGuid();
        dbContext.Users.Add(new User
        {
            Id = requesterId,
            Username = "tester",
            PasswordHash = TestPasswords.Hashed
        });

        var asset = new Asset
        {
            Id = Guid.NewGuid(),
            AssetCode = "TRK-001",
            AssetType = AssetType.Truck
        };
        dbContext.Assets.Add(asset);

        var inventoryItem = new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = "Winch",
            Unit = "PCS",
            ItemType = ItemType.NonConsumable
        };
        dbContext.InventoryItems.Add(inventoryItem);
        await dbContext.SaveChangesAsync();

        var command = new CreateRequestCommand(
            RequestType.MaintenanceIssue,
            requesterId,
            asset.Id,
            "Replace part",
            new[] { new RequestLineInput(inventoryItem.Id, 1, null) });

        await Assert.ThrowsAnyAsync<DomainException>(() => requestService.CreateRequestAsync(command));
    }

    [Fact]
    public async Task Borrow_RejectsConsumableItems()
    {
        await using var dbContext = CreateDbContext();
        var requestService = new RequestService(dbContext);

        var requesterId = Guid.NewGuid();
        dbContext.Users.Add(new User
        {
            Id = requesterId,
            Username = "tester",
            PasswordHash = TestPasswords.Hashed
        });

        var inventoryItem = new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = "Brake Fluid",
            Unit = "L",
            ItemType = ItemType.Consumable
        };
        dbContext.InventoryItems.Add(inventoryItem);
        await dbContext.SaveChangesAsync();

        var command = new CreateRequestCommand(
            RequestType.Borrow,
            requesterId,
            null,
            "Field use",
            new[] { new RequestLineInput(inventoryItem.Id, 1, null) });

        await Assert.ThrowsAnyAsync<DomainException>(() => requestService.CreateRequestAsync(command));
    }

    [Fact]
    public async Task MaintenanceIssue_RejectsInactiveAsset()
    {
        await using var dbContext = CreateDbContext();
        var requestService = new RequestService(dbContext);

        var requesterId = Guid.NewGuid();
        dbContext.Users.Add(new User
        {
            Id = requesterId,
            Username = "tester",
            PasswordHash = TestPasswords.Hashed
        });

        var asset = new Asset
        {
            Id = Guid.NewGuid(),
            AssetCode = "TRK-INACTIVE",
            AssetType = AssetType.Truck,
            Status = AssetStatus.Inactive
        };
        dbContext.Assets.Add(asset);

        var inventoryItem = new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = "Oil",
            Unit = "L",
            ItemType = ItemType.Consumable
        };
        dbContext.InventoryItems.Add(inventoryItem);
        await dbContext.SaveChangesAsync();

        var command = new CreateRequestCommand(
            RequestType.MaintenanceIssue,
            requesterId,
            asset.Id,
            "Routine service",
            new[] { new RequestLineInput(inventoryItem.Id, 1, null) });

        await Assert.ThrowsAnyAsync<DomainException>(() => requestService.CreateRequestAsync(command));
    }

    [Fact]
    public async Task MaintenanceIssue_RejectsInactiveInventory()
    {
        await using var dbContext = CreateDbContext();
        var requestService = new RequestService(dbContext);

        var requesterId = Guid.NewGuid();
        dbContext.Users.Add(new User
        {
            Id = requesterId,
            Username = "tester",
            PasswordHash = TestPasswords.Hashed
        });

        var asset = new Asset
        {
            Id = Guid.NewGuid(),
            AssetCode = "TRK-ACTIVE",
            AssetType = AssetType.Truck
        };
        dbContext.Assets.Add(asset);

        var inventoryItem = new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = "Oil",
            Unit = "L",
            ItemType = ItemType.Consumable,
            IsActive = false
        };
        dbContext.InventoryItems.Add(inventoryItem);
        await dbContext.SaveChangesAsync();

        var command = new CreateRequestCommand(
            RequestType.MaintenanceIssue,
            requesterId,
            asset.Id,
            "Routine service",
            new[] { new RequestLineInput(inventoryItem.Id, 1, null) });

        await Assert.ThrowsAnyAsync<DomainException>(() => requestService.CreateRequestAsync(command));
    }

    [Fact]
    public async Task Adjustment_RequiresReason()
    {
        await using var dbContext = CreateDbContext();
        var requestService = new RequestService(dbContext);

        var requesterId = Guid.NewGuid();
        dbContext.Users.Add(new User
        {
            Id = requesterId,
            Username = "adjuster",
            PasswordHash = TestPasswords.Hashed
        });

        var inventoryItem = new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = "Damaged Part",
            Unit = "PCS",
            ItemType = ItemType.Consumable
        };
        dbContext.InventoryItems.Add(inventoryItem);
        await dbContext.SaveChangesAsync();

        var command = new CreateRequestCommand(
            RequestType.AdjustmentDamageLoss,
            requesterId,
            null,
            null,
            new[] { new RequestLineInput(inventoryItem.Id, 1, null) });

        await Assert.ThrowsAnyAsync<DomainException>(() => requestService.CreateRequestAsync(command));
    }

    [Fact]
    public async Task MaintenanceIssue_AllowsConsumableWithAsset()
    {
        await using var dbContext = CreateDbContext();
        var requestService = new RequestService(dbContext);

        var requesterId = Guid.NewGuid();
        dbContext.Users.Add(new User
        {
            Id = requesterId,
            Username = "tester",
            PasswordHash = TestPasswords.Hashed
        });

        var asset = new Asset
        {
            Id = Guid.NewGuid(),
            AssetCode = "TRK-002",
            AssetType = AssetType.Truck
        };
        dbContext.Assets.Add(asset);

        var inventoryItem = new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = "Grease",
            Unit = "PCS",
            ItemType = ItemType.Consumable
        };
        dbContext.InventoryItems.Add(inventoryItem);
        await dbContext.SaveChangesAsync();

        var command = new CreateRequestCommand(
            RequestType.MaintenanceIssue,
            requesterId,
            asset.Id,
            "Service",
            new[] { new RequestLineInput(inventoryItem.Id, 1, null) });

        var request = await requestService.CreateRequestAsync(command);

        Assert.Equal(RequestType.MaintenanceIssue, request.RequestType);
        Assert.Equal(RequestStatus.Draft, request.Status);
        Assert.Single(request.Lines);
    }

    private static InventoryDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new InventoryDbContext(options);
    }
}
