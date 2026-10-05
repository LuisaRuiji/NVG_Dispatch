using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Modules.Dispatching.Enums;
using NVGInventory.Modules.Dispatching.Services;
using NVGInventory.Modules.ShipmentRequests.Enums;
using NVGInventory.Modules.ShipmentRequests.Services;
using NVGInventory.Security;

namespace NVGInventory.Modules.ShipmentRequests;

[ApiController]
[Route("api/finance/bookings")]
[Authorize(Roles = $"{RoleNames.HeadOfFinance},{RoleNames.Manager}")]
public sealed class BookingFinanceController : ControllerBase
{
    private readonly BookingFinanceService _service;
    private readonly InventoryDbContext _dbContext;

    public BookingFinanceController(BookingFinanceService service, InventoryDbContext dbContext)
    {
        _service = service;
        _dbContext = dbContext;
    }

    [HttpGet("pending")]
    public async Task<ActionResult<IReadOnlyCollection<BookingFinanceResponse>>> Pending(CancellationToken cancellationToken) =>
        Ok((await _dbContext.ShipmentRequests.AsNoTracking()
            .Include(request => request.Customer)
            .Where(request => request.Status == ShipmentRequestStatus.AwaitingFinanceClearance)
            .OrderBy(request => request.CreatedAt)
            .ToListAsync(cancellationToken)).Select(Map));

    [Authorize(Roles = RoleNames.HeadOfFinance)]
    [HttpPost("{requestId:guid}/verify-payment")]
    public async Task<ActionResult<BookingFinanceResponse>> VerifyPayment(Guid requestId, VerifyBookingPaymentRequest request, CancellationToken cancellationToken) =>
        Ok(Map(await _service.VerifyPaymentAsync(requestId, new VerifyBookingPaymentCommand(request.Amount, request.IsDeposit, request.ApproveDepositForDispatch, request.PaymentMethod, request.ReferenceNumber, request.Reason), BuildActor(), cancellationToken)));

    [Authorize(Roles = RoleNames.HeadOfFinance)]
    [HttpPost("{requestId:guid}/clear-credit")]
    public async Task<ActionResult<BookingFinanceResponse>> ClearCredit(Guid requestId, ClearCreditBookingRequest request, CancellationToken cancellationToken) =>
        Ok(Map(await _service.ClearCreditAsync(requestId, new ClearCreditBookingCommand(request.BookingAmount, request.Reason), BuildActor(), cancellationToken)));

    [HttpPost("{requestId:guid}/authorize-exception")]
    public async Task<ActionResult<BookingFinanceResponse>> AuthorizeException(Guid requestId, BookingFinanceReasonRequest request, CancellationToken cancellationToken) =>
        Ok(Map(await _service.AuthorizeExceptionAsync(requestId, request.Reason, BuildActor(), cancellationToken)));

    [Authorize(Roles = RoleNames.HeadOfFinance)]
    [HttpPost("{requestId:guid}/block")]
    public async Task<ActionResult<BookingFinanceResponse>> Block(Guid requestId, BookingFinanceReasonRequest request, CancellationToken cancellationToken) =>
        Ok(Map(await _service.BlockAsync(requestId, request.Reason, BuildActor(), cancellationToken)));

    private DispatchActorContext BuildActor() => new(
        User.GetUserId(), User.IsInRole(RoleNames.Manager), false, false,
        User.IsInRole(RoleNames.HeadOfFinance), false);

    private static BookingFinanceResponse Map(Entities.ShipmentRequest request) => new(
        request.Id,
        request.Status,
        request.FinanceClearanceStatus,
        request.QuotedAmount,
        request.RequiredDepositAmount,
        request.VerifiedPaymentAmount,
        request.VerifiedDepositAmount,
        request.FinanceClearedAt,
        request.FinanceClearanceReason,
        request.FinanceExceptionAt,
        request.FinanceExceptionReason,
        request.Customer?.Name,
        request.BookingNumber,
        request.PickupLocation,
        request.DropoffLocation,
        request.RequestedPickupTime,
        request.Customer?.AccountStatus,
        request.Customer?.CreditStatus,
        request.Customer?.HasOverdueBalance ?? false);
}

public sealed record VerifyBookingPaymentRequest(decimal Amount, bool IsDeposit, bool ApproveDepositForDispatch, string PaymentMethod, string ReferenceNumber, string Reason);
public sealed record ClearCreditBookingRequest(decimal? BookingAmount, string Reason);
public sealed record BookingFinanceReasonRequest(string Reason);
public sealed record BookingFinanceResponse(
    Guid BookingId,
    ShipmentRequestStatus BookingStatus,
    BookingFinanceClearanceStatus FinanceStatus,
    decimal? QuotedAmount,
    decimal? RequiredDepositAmount,
    decimal VerifiedPaymentAmount,
    decimal VerifiedDepositAmount,
    DateTime? ClearedAt,
    string? ClearanceReason,
    DateTime? ExceptionAt,
    string? ExceptionReason,
    string? CustomerName,
    string? BookingNumber,
    string PickupLocation,
    string DropoffLocation,
    DateTime? RequestedPickupTime,
    CustomerAccountStatus? CustomerAccountStatus,
    CustomerCreditStatus? CustomerCreditStatus,
    bool HasOverdueBalance);
