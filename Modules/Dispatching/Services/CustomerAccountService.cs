using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Exceptions;
using NVGInventory.Domain.Services;
using NVGInventory.Modules.Dispatching.Entities;
using NVGInventory.Modules.Dispatching.Enums;

namespace NVGInventory.Modules.Dispatching.Services;

public sealed record CustomerAccountRequestCommand(
    string CompanyName,
    string ContactPerson,
    string ContactEmail,
    string? Phone,
    string? Address,
    string Reason);

public sealed record CustomerCreditTermsCommand(
    CustomerCreditStatus Status,
    decimal? CreditLimit,
    string Reason);

public sealed class CustomerAccountService
{
    private readonly InventoryDbContext _dbContext;
    private readonly IAuditService _auditService;

    public CustomerAccountService(InventoryDbContext dbContext, IAuditService auditService)
    {
        _dbContext = dbContext;
        _auditService = auditService;
    }

    public async Task<Customer> SubmitRequestAsync(
        CustomerAccountRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.CompanyName) ||
            string.IsNullOrWhiteSpace(command.ContactPerson) ||
            string.IsNullOrWhiteSpace(command.ContactEmail))
        {
            throw new BusinessRuleViolationException("Company name, contact person, and contact email are required.");
        }

        var email = command.ContactEmail.Trim();
        var existingEmails = await _dbContext.DispatchCustomers
            .AsNoTracking()
            .Select(customer => customer.ContactEmail)
            .ToListAsync(cancellationToken);
        if (existingEmails.Any(value => string.Equals(value, email, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ConflictDomainException("An account request already exists for this email address.");
        }

        var now = DateTime.UtcNow;
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = command.CompanyName.Trim(),
            ContactPerson = command.ContactPerson.Trim(),
            ContactEmail = email,
            Phone = Normalize(command.Phone),
            Address = Normalize(command.Address),
            AccountStatus = CustomerAccountStatus.PendingReview,
            AccountRequestedAt = now,
            AccountStatusReason = Normalize(command.Reason),
            CreditStatus = CustomerCreditStatus.NotGranted,
            CreatedAt = now
        };
        _dbContext.DispatchCustomers.Add(customer);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return customer;
    }

    public Task<Customer> ApproveAsync(
        Guid customerId,
        string reason,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default) =>
        ChangeAccountStatusAsync(customerId, CustomerAccountStatus.ActivePrepaid, reason, actor, cancellationToken);

    public Task<Customer> RejectAsync(
        Guid customerId,
        string reason,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default) =>
        ChangeAccountStatusAsync(customerId, CustomerAccountStatus.Rejected, reason, actor, cancellationToken);

    public Task<Customer> SuspendAsync(
        Guid customerId,
        string reason,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default) =>
        ChangeAccountStatusAsync(customerId, CustomerAccountStatus.Suspended, reason, actor, cancellationToken);

    public Task<Customer> PutOnHoldAsync(
        Guid customerId,
        string reason,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default) =>
        ChangeAccountStatusAsync(customerId, CustomerAccountStatus.OnHold, reason, actor, cancellationToken);

    public async Task<Customer> ChangeCreditTermsAsync(
        Guid customerId,
        CustomerCreditTermsCommand command,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default)
    {
        if (!actor.IsFinance)
        {
            throw new ForbiddenDomainException("Only Finance can approve, revoke, or place credit terms on hold.");
        }
        RequireReason(command.Reason);
        if (command.Status == CustomerCreditStatus.Approved && (!command.CreditLimit.HasValue || command.CreditLimit <= 0))
        {
            throw new BusinessRuleViolationException("A positive credit limit is required when approving credit terms.");
        }

        var customer = await GetCustomerAsync(customerId, cancellationToken);
        if (customer.AccountStatus is CustomerAccountStatus.PendingReview or CustomerAccountStatus.Rejected or CustomerAccountStatus.Suspended or CustomerAccountStatus.OnHold)
        {
            throw new ConflictDomainException("Manager account approval is required before Finance can grant credit terms.");
        }

        var previousCredit = customer.CreditStatus;
        var previousAccount = customer.AccountStatus;
        customer.CreditStatus = command.Status;
        customer.CreditLimit = command.Status == CustomerCreditStatus.Approved ? command.CreditLimit : null;
        customer.CreditReviewedByUserId = actor.UserId;
        customer.CreditReviewedAt = DateTime.UtcNow;
        customer.CreditTermsReason = command.Reason.Trim();
        customer.AccountStatus = command.Status == CustomerCreditStatus.Approved
            ? CustomerAccountStatus.ActiveCredit
            : CustomerAccountStatus.ActivePrepaid;

        AddHistory(customer, previousAccount, customer.AccountStatus, previousCredit, customer.CreditStatus, actor, command.Reason);
        _auditService.AddEntry(
            actor.UserId,
            AuditActions.CustomerCreditTermsChanged,
            EntityTypes.CustomerAccount,
            customer.Id,
            new { AccountStatus = previousAccount, CreditStatus = previousCredit },
            new { customer.AccountStatus, customer.CreditStatus, customer.CreditLimit },
            actorRole: RoleNames.HeadOfFinance,
            reason: command.Reason);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return customer;
    }

    public async Task<Customer> SetBalanceStatusAsync(
        Guid customerId,
        decimal outstandingBalance,
        bool hasOverdueBalance,
        string reason,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default)
    {
        if (!actor.IsFinance)
        {
            throw new ForbiddenDomainException("Only Finance can update a customer's balance status.");
        }
        if (outstandingBalance < 0) throw new BusinessRuleViolationException("Outstanding balance cannot be negative.");
        RequireReason(reason);
        var customer = await GetCustomerAsync(customerId, cancellationToken);
        var before = new { customer.OutstandingBalance, customer.HasOverdueBalance };
        customer.OutstandingBalance = outstandingBalance;
        customer.HasOverdueBalance = hasOverdueBalance;
        _auditService.AddEntry(
            actor.UserId,
            AuditActions.CustomerCreditTermsChanged,
            EntityTypes.CustomerAccount,
            customer.Id,
            before,
            new { customer.OutstandingBalance, customer.HasOverdueBalance },
            actorRole: RoleNames.HeadOfFinance,
            reason: reason);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return customer;
    }

    private async Task<Customer> ChangeAccountStatusAsync(
        Guid customerId,
        CustomerAccountStatus target,
        string reason,
        DispatchActorContext actor,
        CancellationToken cancellationToken)
    {
        if (!actor.IsManager)
        {
            throw new ForbiddenDomainException("Only a Manager can approve, reject, suspend, or hold customer accounts.");
        }
        RequireReason(reason);
        var customer = await GetCustomerAsync(customerId, cancellationToken);
        var previous = customer.AccountStatus;
        if (previous == target) throw new ConflictDomainException("The customer account is already in that status.");
        if (target == CustomerAccountStatus.Rejected && previous != CustomerAccountStatus.PendingReview)
        {
            throw new ConflictDomainException("Only pending account requests can be rejected.");
        }
        if (target is CustomerAccountStatus.ActiveCredit)
        {
            throw new ForbiddenDomainException("Only Finance can grant active credit terms.");
        }

        customer.AccountStatus = target;
        customer.AccountReviewedByUserId = actor.UserId;
        customer.AccountReviewedAt = DateTime.UtcNow;
        customer.AccountStatusReason = reason.Trim();
        AddHistory(customer, previous, target, customer.CreditStatus, customer.CreditStatus, actor, reason);
        _auditService.AddEntry(
            actor.UserId,
            AuditActions.CustomerAccountStatusChanged,
            EntityTypes.CustomerAccount,
            customer.Id,
            new { AccountStatus = previous },
            new { AccountStatus = target },
            actorRole: RoleNames.Manager,
            reason: reason);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return customer;
    }

    private void AddHistory(
        Customer customer,
        CustomerAccountStatus from,
        CustomerAccountStatus to,
        CustomerCreditStatus fromCredit,
        CustomerCreditStatus toCredit,
        DispatchActorContext actor,
        string reason)
    {
        _dbContext.CustomerAccountHistories.Add(new CustomerAccountHistory
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            FromStatus = from,
            ToStatus = to,
            FromCreditStatus = fromCredit,
            ToCreditStatus = toCredit,
            ActorUserId = actor.UserId,
            ActorRole = actor.IsFinance ? RoleNames.HeadOfFinance : RoleNames.Manager,
            Reason = reason.Trim(),
            ChangedAt = DateTime.UtcNow
        });
    }

    private async Task<Customer> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken) =>
        await _dbContext.DispatchCustomers.FirstOrDefaultAsync(customer => customer.Id == customerId, cancellationToken)
        ?? throw new NotFoundException("Customer account not found.");

    public static void EnsureCanSubmitBooking(Customer customer)
    {
        if (customer.AccountStatus is not (CustomerAccountStatus.ActivePrepaid or CustomerAccountStatus.ActiveCredit))
        {
            throw new ForbiddenDomainException("Only active prepaid or active credit customers can submit bookings.");
        }
    }

    public static void EnsureCanReceiveDispatchRelease(Customer customer)
    {
        EnsureCanSubmitBooking(customer);
        if (customer.AccountStatus == CustomerAccountStatus.ActiveCredit && customer.CreditStatus != CustomerCreditStatus.Approved)
        {
            throw new ConflictDomainException("The customer's credit terms are not active.");
        }
    }

    private static void RequireReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new BusinessRuleViolationException("A reason is required.");
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
