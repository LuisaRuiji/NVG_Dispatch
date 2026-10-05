using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NVGInventory.Domain.Constants;
using NVGInventory.Modules.Dispatching.Services;
using NVGInventory.Security;

namespace NVGInventory.Modules.Dispatching.Controllers;

/// <summary>Finance actions that update a delivered trip's customer payment standing.</summary>
[ApiController]
[Route("api/dispatch/trips/{tripId:guid}/payment-status")]
[Authorize(Roles = RoleNames.HeadOfFinance)]
public sealed class TripPaymentStatusController : ControllerBase
{
    private readonly CustomerAccountService _customerAccounts;

    public TripPaymentStatusController(CustomerAccountService customerAccounts) => _customerAccounts = customerAccounts;

    /// <summary>Records the unpaid final amount and marks the customer overdue.</summary>
    [HttpPost("overdue-balance")]
    public async Task<ActionResult> RecordOverdueBalance(
        Guid tripId,
        RecordOverdueBalanceRequest request,
        CancellationToken cancellationToken)
    {
        await _customerAccounts.RecordOverdueTripBalanceAsync(
            tripId, request.UnpaidFinalBalance, request.Reason,
            new DispatchActorContext(User.GetUserId(), false, false, false, User.IsInRole(RoleNames.HeadOfFinance), false, User.IsInRole(RoleNames.Admin) || User.IsInRole(RoleNames.SuperAdmin)),
            cancellationToken);
        return NoContent();
    }
}

public sealed record RecordOverdueBalanceRequest(decimal UnpaidFinalBalance, string Reason);
