using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NVGInventory.Contracts;
using NVGInventory.Modules.Dispatching.Services;

namespace NVGInventory.Modules.Dispatching.Controllers;

[ApiController]
[Route("api/dispatch/customers")]
[Authorize]
public sealed class DispatchCustomersController : ControllerBase
{
    private readonly DispatchCustomerService _customerService;

    public DispatchCustomersController(DispatchCustomerService customerService)
    {
        _customerService = customerService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<DispatchCustomerSummaryResponse>>> GetCustomers(
        CancellationToken cancellationToken)
    {
        var customers = await _customerService.GetCustomersAsync(cancellationToken);
        var response = customers
            .Select(customer => new DispatchCustomerSummaryResponse(customer.Id, customer.Name))
            .ToList();

        return Ok(response);
    }
}

