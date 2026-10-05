using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Exceptions;
using NVGInventory.Domain.Services;
using NVGInventory.Modules.Dispatching.Enums;
using NVGInventory.Modules.Dispatching.Services;
using NVGInventory.Hubs;
using NVGInventory.Hubs.Events;
using NVGInventory.Modules.ShipmentRequests.Entities;
using NVGInventory.Modules.ShipmentRequests.Enums;

namespace NVGInventory.Modules.ShipmentRequests.Services;

public sealed record VerifyBookingPaymentCommand(
    decimal Amount,
    bool IsDeposit,
    bool ApproveDepositForDispatch,
    string PaymentMethod,
    string ReferenceNumber,
    string Reason);

public sealed record ClearCreditBookingCommand(decimal? BookingAmount, string Reason);

public sealed class BookingFinanceService
{
    private readonly InventoryDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly IServiceScopeFactory? _serviceScopeFactory;

    public BookingFinanceService(InventoryDbContext dbContext, IAuditService auditService, IServiceScopeFactory? serviceScopeFactory = null)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _serviceScopeFactory = serviceScopeFactory;
    }

    public async Task<ShipmentRequest> VerifyPaymentAsync(
        Guid requestId,
        VerifyBookingPaymentCommand command,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default)
    {
        EnsureFinance(actor);
        if (command.Amount <= 0) throw new BusinessRuleViolationException("Payment amount must be greater than zero.");
        RequireReasonAndReference(command.Reason, command.ReferenceNumber);
        var request = await LoadAsync(requestId, cancellationToken);
        if (request.Customer?.AccountStatus != CustomerAccountStatus.ActivePrepaid)
        {
            throw new ConflictDomainException("Payment verification is only used for active prepaid customers.");
        }
        EnsureAwaitingFinance(request);
        var from = request.FinanceClearanceStatus;
        if (command.IsDeposit)
        {
            request.VerifiedDepositAmount += command.Amount;
        }
        else
        {
            request.VerifiedPaymentAmount += command.Amount;
        }

        var fullPaymentVerified = request.QuotedAmount.HasValue && request.QuotedAmount > 0 &&
                                  request.VerifiedPaymentAmount + request.VerifiedDepositAmount >= request.QuotedAmount;
        var approvedDeposit = command.IsDeposit && command.ApproveDepositForDispatch &&
                              (!request.RequiredDepositAmount.HasValue || request.VerifiedDepositAmount >= request.RequiredDepositAmount.Value);
        if (!fullPaymentVerified && !approvedDeposit)
        {
            request.FinanceClearanceStatus = BookingFinanceClearanceStatus.AwaitingPayment;
            request.FinanceClearanceReason = "Payment recorded; full payment or an approved deposit is still required.";
        }
        else
        {
            Clear(request, actor, command.Reason);
        }
        AddHistory(request, from, request.FinanceClearanceStatus, actor, command.Amount, command.PaymentMethod, command.ReferenceNumber, command.Reason);
        AddAudit(request, from, actor, command.Reason, command.ReferenceNumber);
        await _dbContext.SaveChangesAsync(cancellationToken);
        QueueFinanceChanged(request.Id);
        return request;
    }

    public async Task<ShipmentRequest> ClearCreditAsync(
        Guid requestId,
        ClearCreditBookingCommand command,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default)
    {
        EnsureFinance(actor);
        RequireReason(command.Reason);
        var request = await LoadAsync(requestId, cancellationToken);
        var customer = request.Customer!;
        if (customer.AccountStatus != CustomerAccountStatus.ActiveCredit || customer.CreditStatus != CustomerCreditStatus.Approved)
        {
            throw new ConflictDomainException("The customer does not have active Finance-approved credit terms.");
        }
        if (customer.HasOverdueBalance)
        {
            var blockedFrom = request.FinanceClearanceStatus;
            Block(request, "The customer has an overdue balance.");
            AddHistory(request, blockedFrom, request.FinanceClearanceStatus, actor, command.BookingAmount, "CREDIT", null, request.FinanceClearanceReason!);
            AddAudit(request, blockedFrom, actor, request.FinanceClearanceReason!, null);
            await _dbContext.SaveChangesAsync(cancellationToken);
            QueueFinanceChanged(request.Id);
            throw new ConflictDomainException("The customer has an overdue balance. Record an authorized exception before planning.");
        }

        var bookingAmount = command.BookingAmount ?? request.QuotedAmount ?? 0m;
        if (!customer.CreditLimit.HasValue || customer.OutstandingBalance + bookingAmount > customer.CreditLimit.Value)
        {
            var blockedFrom = request.FinanceClearanceStatus;
            Block(request, "The booking would exceed the customer's approved credit limit.");
            AddHistory(request, blockedFrom, request.FinanceClearanceStatus, actor, bookingAmount, "CREDIT", null, request.FinanceClearanceReason!);
            AddAudit(request, blockedFrom, actor, request.FinanceClearanceReason!, null);
            await _dbContext.SaveChangesAsync(cancellationToken);
            QueueFinanceChanged(request.Id);
            throw new ConflictDomainException("The booking exceeds the customer's approved credit limit.");
        }

        var from = request.FinanceClearanceStatus;
        Clear(request, actor, command.Reason);
        AddHistory(request, from, request.FinanceClearanceStatus, actor, bookingAmount, "CREDIT", null, command.Reason);
        AddAudit(request, from, actor, command.Reason, null);
        await _dbContext.SaveChangesAsync(cancellationToken);
        QueueFinanceChanged(request.Id);
        return request;
    }

    public async Task<ShipmentRequest> AuthorizeExceptionAsync(
        Guid requestId,
        string reason,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default)
    {
        if (!actor.IsFinance && !actor.IsManager)
        {
            throw new ForbiddenDomainException("Only Finance or a Manager can authorize a finance exception.");
        }
        RequireReason(reason);
        var request = await LoadAsync(requestId, cancellationToken);
        EnsureAwaitingFinance(request);
        var from = request.FinanceClearanceStatus;
        request.FinanceClearanceStatus = BookingFinanceClearanceStatus.AuthorizedException;
        request.Status = ShipmentRequestStatus.ClearedForPlanning;
        request.FinanceExceptionByUserId = actor.UserId;
        request.FinanceExceptionAt = DateTime.UtcNow;
        request.FinanceExceptionReason = reason.Trim();
        AddHistory(request, from, request.FinanceClearanceStatus, actor, null, null, null, reason);
        _auditService.AddEntry(
            actor.UserId,
            AuditActions.BookingFinanceExceptionApproved,
            EntityTypes.BookingFinanceClearance,
            request.Id,
            new { FinanceStatus = from },
            new { request.FinanceClearanceStatus, request.Status },
            actorRole: actor.IsFinance ? RoleNames.HeadOfFinance : RoleNames.Manager,
            reason: reason);
        await _dbContext.SaveChangesAsync(cancellationToken);
        QueueFinanceChanged(request.Id);
        return request;
    }

    public async Task<ShipmentRequest> BlockAsync(
        Guid requestId,
        string reason,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default)
    {
        EnsureFinance(actor);
        RequireReason(reason);
        var request = await LoadAsync(requestId, cancellationToken);
        var from = request.FinanceClearanceStatus;
        Block(request, reason);
        AddHistory(request, from, request.FinanceClearanceStatus, actor, null, null, null, reason);
        AddAudit(request, from, actor, reason, null);
        await _dbContext.SaveChangesAsync(cancellationToken);
        QueueFinanceChanged(request.Id);
        return request;
    }

    private async Task<ShipmentRequest> LoadAsync(Guid requestId, CancellationToken cancellationToken) =>
        await _dbContext.ShipmentRequests
            .Include(request => request.Customer)
            .FirstOrDefaultAsync(request => request.Id == requestId, cancellationToken)
        ?? throw new NotFoundException("Booking not found.");

    private static void Clear(ShipmentRequest request, DispatchActorContext actor, string reason)
    {
        request.FinanceClearanceStatus = BookingFinanceClearanceStatus.Cleared;
        request.Status = ShipmentRequestStatus.ClearedForPlanning;
        request.FinanceClearedByUserId = actor.UserId;
        request.FinanceClearedAt = DateTime.UtcNow;
        request.FinanceClearanceReason = reason.Trim();
    }

    private static void Block(ShipmentRequest request, string reason)
    {
        request.FinanceClearanceStatus = BookingFinanceClearanceStatus.Blocked;
        request.Status = ShipmentRequestStatus.AwaitingFinanceClearance;
        request.FinanceClearanceReason = reason.Trim();
    }

    private void AddHistory(
        ShipmentRequest request,
        BookingFinanceClearanceStatus from,
        BookingFinanceClearanceStatus to,
        DispatchActorContext actor,
        decimal? amount,
        string? method,
        string? reference,
        string reason)
    {
        _dbContext.BookingFinanceHistories.Add(new BookingFinanceHistory
        {
            Id = Guid.NewGuid(),
            ShipmentRequestId = request.Id,
            FromStatus = from,
            ToStatus = to,
            ActorUserId = actor.UserId,
            ActorRole = actor.IsFinance ? RoleNames.HeadOfFinance : RoleNames.Manager,
            Amount = amount,
            PaymentMethod = method,
            ReferenceNumber = reference,
            Reason = reason.Trim(),
            ChangedAt = DateTime.UtcNow
        });
    }

    private void AddAudit(ShipmentRequest request, BookingFinanceClearanceStatus from, DispatchActorContext actor, string reason, string? reference) =>
        _auditService.AddEntry(
            actor.UserId,
            AuditActions.BookingFinanceClearanceChanged,
            EntityTypes.BookingFinanceClearance,
            request.Id,
            new { FinanceStatus = from },
            new { request.FinanceClearanceStatus, request.Status },
            actorRole: RoleNames.HeadOfFinance,
            reason: reason,
            referenceNumber: reference);

    private static void EnsureAwaitingFinance(ShipmentRequest request)
    {
        if (request.Status is not (ShipmentRequestStatus.AwaitingFinanceClearance or ShipmentRequestStatus.Approved) ||
            request.FinanceClearanceStatus is BookingFinanceClearanceStatus.Cleared or BookingFinanceClearanceStatus.AuthorizedException)
        {
            throw new ConflictDomainException("This booking is not awaiting Finance clearance.");
        }
    }

    private static void EnsureFinance(DispatchActorContext actor)
    {
        if (!actor.IsFinance) throw new ForbiddenDomainException("Only Finance can clear payment or credit for dispatch.");
    }

    private static void RequireReasonAndReference(string reason, string reference)
    {
        RequireReason(reason);
        if (string.IsNullOrWhiteSpace(reference)) throw new BusinessRuleViolationException("A payment reference number is required.");
    }

    private static void RequireReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new BusinessRuleViolationException("A reason is required.");
    }

    private void QueueFinanceChanged(Guid requestId)
    {
        if (_serviceScopeFactory is null) return;
        _ = Task.Run(async () =>
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<BookingFinanceService>>();
            try
            {
                var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
                var request = await db.ShipmentRequests.Include(item => item.Customer).AsNoTracking().FirstOrDefaultAsync(item => item.Id == requestId);
                if (request is null) return;
                var hub = scope.ServiceProvider.GetRequiredService<IHubContext<VaiaDispatchHub, IVaiaDispatchClient>>();
                await hub.Clients.Group(VaiaDispatchHub.DispatchOpsGroup).BookingFinanceChanged(new BookingFinanceChangedEvent(
                    request.Id, request.Status.ToString(), request.FinanceClearanceStatus.ToString(), request.Customer?.Name ?? "Unknown customer", DateTime.UtcNow));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to broadcast booking finance change for request {RequestId}.", requestId);
            }
        });
    }
}
