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
public class MaintenanceIssueFlowTests : SqlServerIntegrationTestBase
{
    public MaintenanceIssueFlowTests(SqlServerIntegrationFixture fixture) : base(fixture)
    {
    }

    [SqlServerFact]
    public async Task MaintenanceIssue_Issue_DeductsStockAndClosesRequest()
    {
        Guid requestId;
        Guid inventoryId;
        Guid ioUserId;
        Guid managerId;

        await using (var setupContext = CreateDbContext())
        {
            var requester = new User
            {
                Id = Guid.NewGuid(),
                Username = "req_user",
                PasswordHash = TestPasswords.Hashed
            };

            var ioUser = new User
            {
                Id = Guid.NewGuid(),
                Username = "io_user",
                PasswordHash = TestPasswords.Hashed
            };

            var manager = new User
            {
                Id = Guid.NewGuid(),
                Username = "manager",
                PasswordHash = TestPasswords.Hashed
            };

            setupContext.Users.AddRange(requester, ioUser, manager);
            var ioRole = await setupContext.Roles.SingleAsync(r => r.Name == RoleNames.InventoryOfficer);
            var managerRole = await setupContext.Roles.SingleAsync(r => r.Name == RoleNames.Manager);
            setupContext.UserRoles.AddRange(
                new UserRole { UserId = ioUser.Id, RoleId = ioRole.Id },
                new UserRole { UserId = manager.Id, RoleId = managerRole.Id });

            var asset = new Asset
            {
                Id = Guid.NewGuid(),
                AssetCode = "TRK-900",
                AssetType = AssetType.Truck
            };

            var item = new InventoryItem
            {
                Id = Guid.NewGuid(),
                Name = "Engine Oil",
                Unit = "L",
                ItemType = ItemType.Consumable,
                Quantity = 100m
            };

            setupContext.Assets.Add(asset);
            setupContext.InventoryItems.Add(item);
            await setupContext.SaveChangesAsync();

            inventoryId = item.Id;
            ioUserId = ioUser.Id;
            managerId = manager.Id;

            var requestService = new RequestService(setupContext);
            var approvalService = new ApprovalService(setupContext, new UserService(setupContext));
            var workflowService = new RequestWorkflowService(
                setupContext,
                requestService,
                approvalService,
                new StockLedgerService(setupContext),
                new UserService(setupContext));

            var request = await requestService.CreateRequestAsync(
                new CreateRequestCommand(
                    RequestType.MaintenanceIssue,
                    requester.Id,
                    asset.Id,
                    "Maintenance",
                    new[] { new RequestLineInput(item.Id, 10m, null) }));

            requestId = request.Id;

            await workflowService.SubmitRequest(requestId);

            var lineId = request.Lines.Single().Id;
            await workflowService.InventoryOfficerReviewAsync(
                requestId,
                ioUserId,
                new[] { (lineId, 10m, (string?)null) },
                null);

            await workflowService.ManagerDecisionAsync(
                requestId,
                managerId,
                ApprovalDecision.Approve,
                null);

            await workflowService.IssueRequest(requestId, ioUserId);
        }

        await using (var verifyContext = CreateDbContext())
        {
            var inventory = await verifyContext.InventoryItems.SingleAsync(i => i.Id == inventoryId);
            var request = await verifyContext.Requests.SingleAsync(r => r.Id == requestId);
            var stockLogs = await verifyContext.StockLogs.Where(log => log.InventoryId == inventoryId).ToListAsync();

            Assert.Equal(90m, inventory.Quantity);
            Assert.Single(stockLogs);
            Assert.Equal(RequestStatus.Closed, request.Status);
        }
    }

    [SqlServerFact]
    public async Task MaintenanceIssue_Issue_StoresUnitCostSnapshot()
    {
        Guid requestId;
        Guid inventoryId;
        Guid ioUserId;
        Guid managerId;

        await using (var setupContext = CreateDbContext())
        {
            var requester = new User
            {
                Id = Guid.NewGuid(),
                Username = "req_cost",
                PasswordHash = TestPasswords.Hashed
            };

            var ioUser = new User
            {
                Id = Guid.NewGuid(),
                Username = "io_cost",
                PasswordHash = TestPasswords.Hashed
            };

            var manager = new User
            {
                Id = Guid.NewGuid(),
                Username = "manager_cost",
                PasswordHash = TestPasswords.Hashed
            };

            setupContext.Users.AddRange(requester, ioUser, manager);
            var ioRole = await setupContext.Roles.SingleAsync(r => r.Name == RoleNames.InventoryOfficer);
            var managerRole = await setupContext.Roles.SingleAsync(r => r.Name == RoleNames.Manager);
            setupContext.UserRoles.AddRange(
                new UserRole { UserId = ioUser.Id, RoleId = ioRole.Id },
                new UserRole { UserId = manager.Id, RoleId = managerRole.Id });

            var asset = new Asset
            {
                Id = Guid.NewGuid(),
                AssetCode = "TRK-COST",
                AssetType = AssetType.Truck
            };

            var item = new InventoryItem
            {
                Id = Guid.NewGuid(),
                Name = "Engine Oil",
                Unit = "L",
                ItemType = ItemType.Consumable,
                Quantity = 50m,
                AverageCost = 25m
            };

            setupContext.Assets.Add(asset);
            setupContext.InventoryItems.Add(item);
            await setupContext.SaveChangesAsync();

            inventoryId = item.Id;
            ioUserId = ioUser.Id;
            managerId = manager.Id;

            var requestService = new RequestService(setupContext);
            var approvalService = new ApprovalService(setupContext, new UserService(setupContext));
            var workflowService = new RequestWorkflowService(
                setupContext,
                requestService,
                approvalService,
                new StockLedgerService(setupContext),
                new UserService(setupContext));

            var request = await requestService.CreateRequestAsync(
                new CreateRequestCommand(
                    RequestType.MaintenanceIssue,
                    requester.Id,
                    asset.Id,
                    "Maintenance",
                    new[] { new RequestLineInput(item.Id, 5m, null) }));

            requestId = request.Id;

            await workflowService.SubmitRequest(requestId);

            var lineId = request.Lines.Single().Id;
            await workflowService.InventoryOfficerReviewAsync(
                requestId,
                ioUserId,
                new[] { (lineId, 5m, (string?)null) },
                null);

            await workflowService.ManagerDecisionAsync(
                requestId,
                managerId,
                ApprovalDecision.Approve,
                null);

            await workflowService.IssueRequest(requestId, ioUserId);
        }

        await using (var verifyContext = CreateDbContext())
        {
            var log = await verifyContext.StockLogs
                .SingleAsync(entry =>
                    entry.RefType == EntityTypes.Request
                    && entry.RefId == requestId
                    && entry.InventoryId == inventoryId
                    && entry.MovementType == StockMovementType.Out);

            Assert.Equal(25m, log.UnitCostSnapshot);
            Assert.Equal(125m, log.TotalCostSnapshot);
        }
    }

    [SqlServerFact]
    public async Task MaintenanceIssue_Issue_IsIdempotent()
    {
        Guid requestId;
        Guid inventoryId;
        Guid ioUserId;
        Guid managerId;

        await using (var setupContext = CreateDbContext())
        {
            var requester = new User
            {
                Id = Guid.NewGuid(),
                Username = "req_issue_idempotent",
                PasswordHash = TestPasswords.Hashed
            };

            var ioUser = new User
            {
                Id = Guid.NewGuid(),
                Username = "io_issue_idempotent",
                PasswordHash = TestPasswords.Hashed
            };

            var manager = new User
            {
                Id = Guid.NewGuid(),
                Username = "mgr_issue_idempotent",
                PasswordHash = TestPasswords.Hashed
            };

            setupContext.Users.AddRange(requester, ioUser, manager);
            var ioRole = await setupContext.Roles.SingleAsync(r => r.Name == RoleNames.InventoryOfficer);
            var managerRole = await setupContext.Roles.SingleAsync(r => r.Name == RoleNames.Manager);
            setupContext.UserRoles.AddRange(
                new UserRole { UserId = ioUser.Id, RoleId = ioRole.Id },
                new UserRole { UserId = manager.Id, RoleId = managerRole.Id });

            var asset = new Asset
            {
                Id = Guid.NewGuid(),
                AssetCode = "TRK-IDEMPOTENT",
                AssetType = AssetType.Truck
            };

            var item = new InventoryItem
            {
                Id = Guid.NewGuid(),
                Name = "Engine Oil",
                Unit = "L",
                ItemType = ItemType.Consumable,
                Quantity = 10m
            };

            setupContext.Assets.Add(asset);
            setupContext.InventoryItems.Add(item);
            await setupContext.SaveChangesAsync();

            inventoryId = item.Id;
            ioUserId = ioUser.Id;
            managerId = manager.Id;

            var requestService = new RequestService(setupContext);
            var approvalService = new ApprovalService(setupContext, new UserService(setupContext));
            var workflowService = new RequestWorkflowService(
                setupContext,
                requestService,
                approvalService,
                new StockLedgerService(setupContext),
                new UserService(setupContext));

            var request = await requestService.CreateRequestAsync(
                new CreateRequestCommand(
                    RequestType.MaintenanceIssue,
                    requester.Id,
                    asset.Id,
                    "Maintenance",
                    new[] { new RequestLineInput(item.Id, 5m, null) }));

            requestId = request.Id;

            await workflowService.SubmitRequest(requestId);

            var lineId = request.Lines.Single().Id;
            await workflowService.InventoryOfficerReviewAsync(
                requestId,
                ioUserId,
                new[] { (lineId, 5m, (string?)null) },
                null);

            await workflowService.ManagerDecisionAsync(
                requestId,
                managerId,
                ApprovalDecision.Approve,
                null);
        }

        await using (var issueContext = CreateDbContext())
        {
            var workflowService = new RequestWorkflowService(
                issueContext,
                new RequestService(issueContext),
                new ApprovalService(issueContext, new UserService(issueContext)),
                new StockLedgerService(issueContext),
                new UserService(issueContext));

            await workflowService.IssueRequest(requestId, ioUserId);
        }

        await using (var bypassContext = CreateDbContext())
        {
            var request = await bypassContext.Requests.SingleAsync(r => r.Id == requestId);
            request.Status = RequestStatus.Approved;
            request.UpdatedAt = DateTime.UtcNow;
            await bypassContext.SaveChangesAsync();
        }

        await using (var secondIssueContext = CreateDbContext())
        {
            var workflowService = new RequestWorkflowService(
                secondIssueContext,
                new RequestService(secondIssueContext),
                new ApprovalService(secondIssueContext, new UserService(secondIssueContext)),
                new StockLedgerService(secondIssueContext),
                new UserService(secondIssueContext));

            var ex = await Assert.ThrowsAnyAsync<DomainException>(async () =>
            {
                await workflowService.IssueRequest(requestId, ioUserId);
            });

            Assert.Equal("Request already issued.", ex.Message);
        }

        await using (var verifyContext = CreateDbContext())
        {
            var inventory = await verifyContext.InventoryItems.SingleAsync(i => i.Id == inventoryId);
            var stockLogs = await verifyContext.StockLogs
                .Where(log => log.RefType == EntityTypes.Request && log.RefId == requestId)
                .ToListAsync();

            Assert.Equal(5m, inventory.Quantity);
            Assert.Single(stockLogs);
        }
    }
}
