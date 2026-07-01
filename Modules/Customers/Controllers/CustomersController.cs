using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NVGInventory.Contracts;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Exceptions;
using NVGInventory.Domain.Services;
using NVGInventory.Modules.Dispatching.Entities;

namespace NVGInventory.Modules.Customers.Controllers;

[ApiController]
[Route("api/admin/customers")]
[Authorize(Roles = RoleNames.Dispatcher + "," + RoleNames.Manager + "," + RoleNames.Admin + "," + RoleNames.SuperAdmin)]
public sealed class CustomersController : ControllerBase
{
    private readonly InventoryDbContext _dbContext;
    private readonly UserService _userService;

    public CustomersController(InventoryDbContext dbContext, UserService userService)
    {
        _dbContext = dbContext;
        _userService = userService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<CustomerListItemResponse>>> GetCustomers(
        CancellationToken cancellationToken)
    {
        var customers = await _dbContext.DispatchCustomers
            .AsNoTracking()
            .OrderByDescending(customer => customer.CreatedAt)
            .Select(customer => new CustomerListItemResponse(
                customer.Id,
                customer.Name,
                customer.ContactPerson,
                customer.ContactEmail,
                customer.Phone,
                customer.CreatedAt))
            .ToListAsync(cancellationToken);

        return Ok(customers);
    }

    [HttpPost]
    public async Task<ActionResult<CustomerListItemResponse>> CreateCustomer(
        CreateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BusinessRuleViolationException("Customer name is required.");
        }

        var now = DateTime.UtcNow;
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            ContactPerson = string.IsNullOrWhiteSpace(request.ContactPerson) ? null : request.ContactPerson.Trim(),
            ContactEmail = string.IsNullOrWhiteSpace(request.ContactEmail) ? null : request.ContactEmail.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            CreatedAt = now
        };

        _dbContext.DispatchCustomers.Add(customer);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new CustomerListItemResponse(
            customer.Id,
            customer.Name,
            customer.ContactPerson,
            customer.ContactEmail,
            customer.Phone,
            customer.CreatedAt));
    }

    [HttpPost("{customerId:guid}/users")]
    public async Task<ActionResult<CreateCustomerUserResponse>> CreateCustomerUser(
        Guid customerId,
        CreateCustomerUserRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new BusinessRuleViolationException("Email is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new BusinessRuleViolationException("Password is required.");
        }

        var customer = await _dbContext.DispatchCustomers
            .FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);

        if (customer is null)
        {
            throw new NotFoundException("Customer not found.");
        }

        var email = request.Email.Trim();
        var user = await _userService.CreateUserAsync(
            new CreateUserCommand(email, request.Password, email),
            cancellationToken);

        user.CustomerId = customerId;
        user.MustChangePassword = true;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _userService.AssignRoleAsync(user.Id, RoleNames.Customer, cancellationToken);

        return Ok(new CreateCustomerUserResponse(
            user.Id,
            user.Email ?? email,
            RoleNames.Customer,
            customerId));
    }
}
