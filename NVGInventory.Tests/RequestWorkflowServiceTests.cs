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
public class RequestWorkflowServiceTests : SqlServerIntegrationTestBase
{
    public RequestWorkflowServiceTests(SqlServerIntegrationFixture fixture) : base(fixture)
    {
    }

    [SqlServerFact]
    public async Task IssueMaintenanceIssue_Throws_WhenNotApproved()
    {
        Guid requestId;
        Guid actorId;

        await using (var setupContext = CreateDbContext())
        {
            var requester = new User
            {
                Id = Guid.NewGuid(),
                Username = "requester",
                PasswordHash = TestPasswords.Hashed
            };

            var actor = new User
            {
                Id = Guid.NewGuid(),
                Username = "inventory_officer",
                PasswordHash = TestPasswords.Hashed
            };

            setupContext.Users.AddRange(requester, actor);

            var role = await setupContext.Roles.SingleAsync(r => r.Name == RoleNames.InventoryOfficer);
            setupContext.UserRoles.Add(new UserRole
            {
                UserId = actor.Id,
                RoleId = role.Id
            });

            var asset = new Asset
            {
                Id = Guid.NewGuid(),
                AssetCode = "TRK-200",
                AssetType = AssetType.Truck
            };

            var inventoryItem = new InventoryItem
            {
                Id = Guid.NewGuid(),
                Name = "Engine Oil",
                Unit = "L",
                ItemType = ItemType.Consumable,
                Quantity = 5m
            };

            setupContext.Assets.Add(asset);
            setupContext.InventoryItems.Add(inventoryItem);
            await setupContext.SaveChangesAsync();

            var requestService = new RequestService(setupContext);
            var approvalService = new ApprovalService(setupContext, new UserService(setupContext));
            var stockLedgerService = new StockLedgerService(setupContext);
            var workflowService = new RequestWorkflowService(
                setupContext,
                requestService,
                approvalService,
                stockLedgerService,
                new UserService(setupContext));

            var submitResult = await workflowService.SubmitMaintenanceIssueAsync(
                new SubmitMaintenanceIssueCommand(
                    requester.Id,
                    asset.Id,
                    "Service",
                    new[] { new RequestLineInput(inventoryItem.Id, 1m, null) }));

            requestId = submitResult.RequestId;
            actorId = actor.Id;
        }

        await using (var context = CreateDbContext())
        {
            var requestService = new RequestService(context);
            var approvalService = new ApprovalService(context, new UserService(context));
            var stockLedgerService = new StockLedgerService(context);
            var workflowService = new RequestWorkflowService(
                context,
                requestService,
                approvalService,
                stockLedgerService,
                new UserService(context));

            await Assert.ThrowsAnyAsync<DomainException>(async () =>
            {
                await workflowService.IssueMaintenanceIssueAsync(
                    new IssueRequestCommand(requestId, actorId));
            });
        }
    }
}
