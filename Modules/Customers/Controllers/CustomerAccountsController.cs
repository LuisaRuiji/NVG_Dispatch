using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Modules.Dispatching.Enums;
using NVGInventory.Modules.Dispatching.Services;
using NVGInventory.Security;

namespace NVGInventory.Modules.Customers.Controllers;

[ApiController]
[Route("api/customer-accounts")]
public sealed class CustomerAccountsController : ControllerBase
{
    private readonly CustomerAccountService _service;
    private readonly InventoryDbContext _dbContext;

    public CustomerAccountsController(CustomerAccountService service, InventoryDbContext dbContext)
    {
        _service = service;
        _dbContext = dbContext;
    }

    [AllowAnonymous]
    [HttpPost("requests")]
    public async Task<ActionResult<CustomerAccountResponse>> SubmitRequest(
        CustomerAccountRequest request,
        CancellationToken cancellationToken)
    {
        var customer = await _service.SubmitRequestAsync(
            new CustomerAccountRequestCommand(request.CompanyName, request.ContactPerson, request.ContactEmail, request.Phone, request.Address, request.Reason),
            cancellationToken);
        return Ok(Map(customer));
    }

    [Authorize(Roles = $"{RoleNames.Manager},{RoleNames.HeadOfFinance},{RoleNames.Admin},{RoleNames.SuperAdmin}")]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<CustomerAccountResponse>>> GetAccounts(CancellationToken cancellationToken) =>
        Ok((await _dbContext.DispatchCustomers.AsNoTracking().OrderBy(customer => customer.Name).ToListAsync(cancellationToken)).Select(Map));

    [Authorize(Roles = RoleNames.Manager)]
    [HttpPost("{customerId:guid}/approve")]
    public async Task<ActionResult<CustomerAccountResponse>> Approve(Guid customerId, ReasonRequest request, CancellationToken cancellationToken) =>
        Ok(Map(await _service.ApproveAsync(customerId, request.Reason, BuildActor(), cancellationToken)));

    [Authorize(Roles = RoleNames.Manager)]
    [HttpPost("{customerId:guid}/reject")]
    public async Task<ActionResult<CustomerAccountResponse>> Reject(Guid customerId, ReasonRequest request, CancellationToken cancellationToken) =>
        Ok(Map(await _service.RejectAsync(customerId, request.Reason, BuildActor(), cancellationToken)));

    [Authorize(Roles = RoleNames.Manager)]
    [HttpPost("{customerId:guid}/suspend")]
    public async Task<ActionResult<CustomerAccountResponse>> Suspend(Guid customerId, ReasonRequest request, CancellationToken cancellationToken) =>
        Ok(Map(await _service.SuspendAsync(customerId, request.Reason, BuildActor(), cancellationToken)));

    [Authorize(Roles = RoleNames.Manager)]
    [HttpPost("{customerId:guid}/hold")]
    public async Task<ActionResult<CustomerAccountResponse>> Hold(Guid customerId, ReasonRequest request, CancellationToken cancellationToken) =>
        Ok(Map(await _service.PutOnHoldAsync(customerId, request.Reason, BuildActor(), cancellationToken)));

    [Authorize(Roles = RoleNames.HeadOfFinance)]
    [HttpPost("{customerId:guid}/credit-terms")]
    public async Task<ActionResult<CustomerAccountResponse>> CreditTerms(Guid customerId, CustomerCreditTermsRequest request, CancellationToken cancellationToken) =>
        Ok(Map(await _service.ChangeCreditTermsAsync(customerId, new CustomerCreditTermsCommand(request.Status, request.CreditLimit, request.Reason), BuildActor(), cancellationToken)));

    [Authorize(Roles = RoleNames.HeadOfFinance)]
    [HttpPost("{customerId:guid}/balance-status")]
    public async Task<ActionResult<CustomerAccountResponse>> BalanceStatus(Guid customerId, CustomerBalanceStatusRequest request, CancellationToken cancellationToken) =>
        Ok(Map(await _service.SetBalanceStatusAsync(customerId, request.OutstandingBalance, request.HasOverdueBalance, request.Reason, BuildActor(), cancellationToken)));

    private DispatchActorContext BuildActor() => new(
        User.GetUserId(),
        User.IsInRole(RoleNames.Manager),
        User.IsInRole(RoleNames.Dispatcher),
        User.IsInRole(RoleNames.Driver),
        User.IsInRole(RoleNames.HeadOfFinance),
        User.IsInRole(RoleNames.Ceo),
        User.IsInRole(RoleNames.Admin));

    private static CustomerAccountResponse Map(NVGInventory.Modules.Dispatching.Entities.Customer customer) => new(
        customer.Id,
        customer.Name,
        customer.ContactPerson,
        customer.ContactEmail,
        customer.Phone,
        customer.AccountStatus,
        customer.CreditStatus,
        customer.CreditLimit,
        customer.OutstandingBalance,
        customer.HasOverdueBalance,
        customer.AccountRequestedAt,
        customer.AccountReviewedAt,
        customer.AccountStatusReason);
}

public sealed record CustomerAccountRequest(string CompanyName, string ContactPerson, string ContactEmail, string? Phone, string? Address, string Reason);
public sealed record ReasonRequest(string Reason);
public sealed record CustomerCreditTermsRequest(CustomerCreditStatus Status, decimal? CreditLimit, string Reason);
public sealed record CustomerBalanceStatusRequest(decimal OutstandingBalance, bool HasOverdueBalance, string Reason);
public sealed record CustomerAccountResponse(
    Guid Id,
    string Name,
    string? ContactPerson,
    string? ContactEmail,
    string? Phone,
    CustomerAccountStatus AccountStatus,
    CustomerCreditStatus CreditStatus,
    decimal? CreditLimit,
    decimal OutstandingBalance,
    bool HasOverdueBalance,
    DateTime AccountRequestedAt,
    DateTime? AccountReviewedAt,
    string? StatusReason);
