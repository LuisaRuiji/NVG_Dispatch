using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Entities;
using NVGInventory.Domain.Enums;
using NVGInventory.Domain.Services;

namespace NVGInventory.Data;

internal static class DevSeedData
{
    public static async Task EnsureSeededAsync(IServiceProvider services, IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        var devPassword = configuration["Seed:DevPassword"];
        if (string.IsNullOrWhiteSpace(devPassword))
        {
            throw new InvalidOperationException("Seed:DevPassword must be configured for dev seeding.");
        }

        var seedUsers = new[]
        {
            new { Username = "io1", RoleName = RoleNames.InventoryOfficer },
            new { Username = "mgr1", RoleName = RoleNames.Manager },
            new { Username = "drv1", RoleName = RoleNames.Driver },
            new { Username = "admin1", RoleName = RoleNames.Admin },
            new { Username = "superadmin1", RoleName = RoleNames.SuperAdmin }
        };

        foreach (var seed in seedUsers)
        {
            var exists = await dbContext.Users.AnyAsync(u => u.Username == seed.Username, cancellationToken);
            if (exists)
            {
                continue;
            }

            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = seed.Username,
                Email = null,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(devPassword)
            };

            dbContext.Users.Add(user);

            var role = await dbContext.Roles.FirstOrDefaultAsync(r => r.Name == seed.RoleName, cancellationToken);
            if (role is not null)
            {
                dbContext.UserRoles.Add(new UserRole
                {
                    UserId = user.Id,
                    RoleId = role.Id
                });
            }
        }

        await EnsureSampleRequestsAsync(scope.ServiceProvider, dbContext, devPassword, cancellationToken);

        if (dbContext.ChangeTracker.HasChanges())
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task EnsureSampleRequestsAsync(
        IServiceProvider serviceProvider,
        InventoryDbContext dbContext,
        string devPassword,
        CancellationToken cancellationToken)
    {
        var requester = await dbContext.Users.FirstOrDefaultAsync(u => u.Username == "req1", cancellationToken);
        if (requester is null)
        {
            requester = new User
            {
                Id = Guid.NewGuid(),
                Username = "req1",
                Email = null,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(devPassword)
            };
            dbContext.Users.Add(requester);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var ioUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Username == "io1", cancellationToken);
        var managerUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Username == "mgr1", cancellationToken);
        if (ioUser is null || managerUser is null)
        {
            return;
        }

        var asset = await dbContext.Assets.FirstOrDefaultAsync(a => a.AssetCode == "TRK-DEV-1", cancellationToken);
        if (asset is null)
        {
            asset = new Asset
            {
                Id = Guid.NewGuid(),
                AssetCode = "TRK-DEV-1",
                AssetType = AssetType.Truck,
                Status = AssetStatus.Active,
                CreatedAt = DateTime.UtcNow
            };
            dbContext.Assets.Add(asset);
        }

        var consumable = await dbContext.InventoryItems
            .FirstOrDefaultAsync(i => i.Name == "Dev Engine Oil", cancellationToken);
        if (consumable is null)
        {
            consumable = new InventoryItem
            {
                Id = Guid.NewGuid(),
                Name = "Dev Engine Oil",
                Unit = "L",
                ItemType = ItemType.Consumable,
                Quantity = 100m,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            dbContext.InventoryItems.Add(consumable);
        }

        if (dbContext.ChangeTracker.HasChanges())
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var auditService = new AuditService(
            dbContext,
            serviceProvider.GetRequiredService<ILogger<AuditService>>(),
            serviceProvider.GetRequiredService<IHttpContextAccessor>());
        var requestService = new RequestService(dbContext, auditService);
        var approvalService = new ApprovalService(dbContext, new UserService(dbContext));
        var workflowService = new RequestWorkflowService(
            dbContext,
            requestService,
            approvalService,
            new StockLedgerService(dbContext),
            new UserService(dbContext),
            auditService,
            serviceProvider.GetRequiredService<ILogger<RequestWorkflowService>>(),
            serviceProvider.GetRequiredService<IHttpContextAccessor>());

        const string pendingIoPurpose = "DEV_SAMPLE_PENDING_IO";
        const string pendingManagerPurpose = "DEV_SAMPLE_PENDING_MANAGER";

        var hasPendingIo = await dbContext.Requests.AnyAsync(r => r.Purpose == pendingIoPurpose, cancellationToken);
        if (!hasPendingIo)
        {
            await workflowService.SubmitMaintenanceIssueAsync(
                new SubmitMaintenanceIssueCommand(
                    requester.Id,
                    asset.Id,
                    pendingIoPurpose,
                    new[] { new RequestLineInput(consumable.Id, 5m, null) }),
                cancellationToken);
        }

        var hasPendingManager = await dbContext.Requests.AnyAsync(r => r.Purpose == pendingManagerPurpose, cancellationToken);
        if (!hasPendingManager)
        {
            var draft = await requestService.CreateRequestAsync(
                new CreateRequestCommand(
                    RequestType.MaintenanceIssue,
                    requester.Id,
                    asset.Id,
                    pendingManagerPurpose,
                    new[] { new RequestLineInput(consumable.Id, 3m, null) }),
                RequestStatus.Draft,
                cancellationToken);

            await workflowService.SubmitRequest(draft.Id, cancellationToken);

            var requestLineId = draft.Lines.Single().Id;
            await workflowService.InventoryOfficerReviewAsync(
                draft.Id,
                ioUser.Id,
                new[] { (requestLineId, 3m, (string?)null) },
                "seeded",
                cancellationToken);
        }
    }
}
