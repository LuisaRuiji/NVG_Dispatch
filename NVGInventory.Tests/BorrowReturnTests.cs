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
public class BorrowReturnTests : SqlServerIntegrationTestBase
{
    public BorrowReturnTests(SqlServerIntegrationFixture fixture) : base(fixture)
    {
    }

    private sealed record BorrowSetup(
        Guid RequestId,
        Guid LoanId,
        Guid LoanLineId,
        Guid InventoryId,
        Guid IoUserId,
        Guid ManagerId,
        decimal RequestedQty,
        decimal InitialQuantity);

    private async Task<BorrowSetup> CreateBorrowIssuedAsync(decimal requestedQty = 2m, decimal initialQuantity = 5m)
    {
        Guid requestId;
        Guid loanId;
        Guid loanLineId;
        Guid inventoryId;
        Guid ioUserId;
        Guid managerId;

        await using (var setupContext = CreateDbContext())
        {
            var requester = new User
            {
                Id = Guid.NewGuid(),
                Username = "borrower",
                PasswordHash = TestPasswords.Hashed
            };

            var ioUser = new User
            {
                Id = Guid.NewGuid(),
                Username = "io_user2",
                PasswordHash = TestPasswords.Hashed
            };

            var manager = new User
            {
                Id = Guid.NewGuid(),
                Username = "manager1",
                PasswordHash = TestPasswords.Hashed
            };

            setupContext.Users.AddRange(requester, ioUser, manager);
            var ioRole = await setupContext.Roles.SingleAsync(r => r.Name == RoleNames.InventoryOfficer);
            var managerRole = await setupContext.Roles.SingleAsync(r => r.Name == RoleNames.Manager);
            setupContext.UserRoles.AddRange(
                new UserRole { UserId = ioUser.Id, RoleId = ioRole.Id },
                new UserRole { UserId = manager.Id, RoleId = managerRole.Id });

            var item = new InventoryItem
            {
                Id = Guid.NewGuid(),
                Name = "Portable Winch",
                Unit = "PCS",
                ItemType = ItemType.NonConsumable,
                Quantity = initialQuantity
            };

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
                    RequestType.Borrow,
                    requester.Id,
                    null,
                    "Borrow winch",
                    new[] { new RequestLineInput(item.Id, requestedQty, null) }));

            requestId = request.Id;
            var requestLineId = request.Lines.Single().Id;

            await workflowService.SubmitRequest(requestId);

            await workflowService.InventoryOfficerReviewAsync(
                requestId,
                ioUserId,
                new[] { (requestLineId, requestedQty, (string?)null) },
                null);

            await workflowService.ManagerDecisionAsync(
                requestId,
                managerId,
                ApprovalDecision.Approve,
                null);

            await workflowService.IssueRequest(requestId, ioUserId);

            loanId = await setupContext.Loans.Where(l => l.RequestId == requestId).Select(l => l.Id).SingleAsync();
            loanLineId = await setupContext.LoanLines.Where(l => l.LoanId == loanId).Select(l => l.Id).SingleAsync();
        }

        return new BorrowSetup(requestId, loanId, loanLineId, inventoryId, ioUserId, managerId, requestedQty, initialQuantity);
    }

    [SqlServerFact]
    public async Task InventoryOfficerReview_RejectsQtyApprovedOverRequested()
    {
        Guid requestId;
        Guid ioUserId;

        await using (var setupContext = CreateDbContext())
        {
            var requester = new User
            {
                Id = Guid.NewGuid(),
                Username = "requester_io",
                PasswordHash = TestPasswords.Hashed
            };

            var ioUser = new User
            {
                Id = Guid.NewGuid(),
                Username = "io_user",
                PasswordHash = TestPasswords.Hashed
            };

            setupContext.Users.AddRange(requester, ioUser);
            var ioRole = await setupContext.Roles.SingleAsync(r => r.Name == RoleNames.InventoryOfficer);
            setupContext.UserRoles.Add(new UserRole { UserId = ioUser.Id, RoleId = ioRole.Id });

            var asset = new Asset
            {
                Id = Guid.NewGuid(),
                AssetCode = "TRK-400",
                AssetType = AssetType.Truck
            };

            var item = new InventoryItem
            {
                Id = Guid.NewGuid(),
                Name = "Lubricant",
                Unit = "L",
                ItemType = ItemType.Consumable,
                Quantity = 10m
            };

            setupContext.Assets.Add(asset);
            setupContext.InventoryItems.Add(item);
            await setupContext.SaveChangesAsync();

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
                    "Service",
                    new[] { new RequestLineInput(item.Id, 2m, null) }));

            requestId = request.Id;
            ioUserId = ioUser.Id;

            await workflowService.SubmitRequest(requestId);
        }

        await using (var context = CreateDbContext())
        {
            var requestService = new RequestService(context);
            var approvalService = new ApprovalService(context, new UserService(context));
            var workflowService = new RequestWorkflowService(
                context,
                requestService,
                approvalService,
                new StockLedgerService(context),
                new UserService(context));

            var requestLineId = await context.RequestLines
                .Where(line => line.RequestId == requestId)
                .Select(line => line.Id)
                .SingleAsync();

            await Assert.ThrowsAnyAsync<DomainException>(async () =>
            {
                await workflowService.InventoryOfficerReviewAsync(
                    requestId,
                    ioUserId,
                    new[] { (requestLineId, 5m, (string?)null) },
                    null);
            });

        }
    }

    [SqlServerFact]
    public async Task BorrowIssue_CreatesLoanAndClosesRequest()
    {
        var setup = await CreateBorrowIssuedAsync();

        await using (var verifyContext = CreateDbContext())
        {
            var request = await verifyContext.Requests.SingleAsync(r => r.Id == setup.RequestId);
            var loan = await verifyContext.Loans.SingleAsync(l => l.Id == setup.LoanId);
            var loanLines = await verifyContext.LoanLines.Where(l => l.LoanId == setup.LoanId).ToListAsync();
            var inventory = await verifyContext.InventoryItems.SingleAsync(i => i.Id == setup.InventoryId);

            Assert.Equal(RequestStatus.Closed, request.Status);
            Assert.Equal(LoanStatus.Open, loan.Status);
            Assert.Single(loanLines);
            Assert.Equal(setup.RequestedQty, loanLines[0].QtyIssued);
            Assert.Equal(0m, loanLines[0].QtyReturned);
            Assert.Equal(setup.InitialQuantity - setup.RequestedQty, inventory.Quantity);

        }
    }

    [SqlServerFact]
    public async Task LoanReturn_GoodAddsStock_DamagedDoesNot_AndClosesWhenFullyReturned()
    {
        var setup = await CreateBorrowIssuedAsync();

        await using (var returnContext = CreateDbContext())
        {
            var loanWorkflowService = new LoanWorkflowService(
                returnContext,
                new StockLedgerService(returnContext),
                new UserService(returnContext));

            var status = await loanWorkflowService.ReturnLoanAsync(
                setup.LoanId,
                setup.IoUserId,
                new[] { new ReturnLoanLineInput(setup.LoanLineId, 1m, ReturnCondition.Good, null) });

            Assert.Equal(LoanStatus.PartiallyReturned, status);
        }

        await using (var verifyContext = CreateDbContext())
        {
            var inventory = await verifyContext.InventoryItems.SingleAsync(i => i.Id == setup.InventoryId);
            var loan = await verifyContext.Loans.SingleAsync(l => l.Id == setup.LoanId);

            Assert.Equal(setup.InitialQuantity - setup.RequestedQty + 1m, inventory.Quantity);
            Assert.Equal(LoanStatus.PartiallyReturned, loan.Status);
        }

        await using (var returnContext = CreateDbContext())
        {
            var loanWorkflowService = new LoanWorkflowService(
                returnContext,
                new StockLedgerService(returnContext),
                new UserService(returnContext));

            var status = await loanWorkflowService.ReturnLoanAsync(
                setup.LoanId,
                setup.IoUserId,
                new[] { new ReturnLoanLineInput(setup.LoanLineId, 1m, ReturnCondition.Damaged, null) });

            Assert.Equal(LoanStatus.Closed, status);
        }

        await using (var verifyContext = CreateDbContext())
        {
            var inventory = await verifyContext.InventoryItems.SingleAsync(i => i.Id == setup.InventoryId);
            var returns = await verifyContext.LoanLineReturns.Where(r => r.LoanLineId == setup.LoanLineId).ToListAsync();
            var loan = await verifyContext.Loans.SingleAsync(l => l.Id == setup.LoanId);

            Assert.Equal(setup.InitialQuantity - setup.RequestedQty + 1m, inventory.Quantity);
            Assert.Equal(2, returns.Count);
            Assert.Contains(returns, r => r.Condition == ReturnCondition.Good);
            Assert.Contains(returns, r => r.Condition == ReturnCondition.Damaged);
            Assert.Equal(LoanStatus.Closed, loan.Status);

        }
    }

    [SqlServerFact]
    public async Task LoanReturn_CannotExceedIssued()
    {
        var setup = await CreateBorrowIssuedAsync();

        await using (var returnContext = CreateDbContext())
        {
            var loanWorkflowService = new LoanWorkflowService(
                returnContext,
                new StockLedgerService(returnContext),
                new UserService(returnContext));

            await Assert.ThrowsAnyAsync<DomainException>(async () =>
            {
                await loanWorkflowService.ReturnLoanAsync(
                    setup.LoanId,
                    setup.IoUserId,
                    new[] { new ReturnLoanLineInput(setup.LoanLineId, 3m, ReturnCondition.Good, null) });
            });
        }

        await using (var cleanupContext = CreateDbContext())
        {
        }
    }

    [SqlServerFact]
    public async Task LoanReturn_DoubleIncrementFails_AndDoesNotOverReturn()
    {
        var setup = await CreateBorrowIssuedAsync(5m, 5m);

        await using (var returnContext = CreateDbContext())
        {
            var loanWorkflowService = new LoanWorkflowService(
                returnContext,
                new StockLedgerService(returnContext),
                new UserService(returnContext));

            await loanWorkflowService.ReturnLoanAsync(
                setup.LoanId,
                setup.IoUserId,
                new[] { new ReturnLoanLineInput(setup.LoanLineId, 3m, ReturnCondition.Good, null) });
        }

        await using (var returnContext = CreateDbContext())
        {
            var loanWorkflowService = new LoanWorkflowService(
                returnContext,
                new StockLedgerService(returnContext),
                new UserService(returnContext));

            await Assert.ThrowsAnyAsync<DomainException>(async () =>
            {
                await loanWorkflowService.ReturnLoanAsync(
                    setup.LoanId,
                    setup.IoUserId,
                    new[] { new ReturnLoanLineInput(setup.LoanLineId, 3m, ReturnCondition.Good, null) });
            });
        }

        await using (var verifyContext = CreateDbContext())
        {
            var inventory = await verifyContext.InventoryItems.SingleAsync(i => i.Id == setup.InventoryId);
            var loanLine = await verifyContext.LoanLines.SingleAsync(l => l.Id == setup.LoanLineId);
            var loan = await verifyContext.Loans.SingleAsync(l => l.Id == setup.LoanId);

            Assert.Equal(3m, loanLine.QtyReturned);
            Assert.Equal(3m, inventory.Quantity);
            Assert.Equal(LoanStatus.PartiallyReturned, loan.Status);

        }
    }
}
