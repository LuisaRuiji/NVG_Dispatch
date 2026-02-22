using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NVGInventory.Contracts;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Services;
using NVGInventory.Security;

namespace NVGInventory.Controllers;

[ApiController]
[Route("api/suppliers")]
[Authorize]
public sealed class SuppliersController : ControllerBase
{
    private readonly SupplierService _supplierService;

    public SuppliersController(SupplierService supplierService)
    {
        _supplierService = supplierService;
    }

    [HttpGet]
    [Authorize(Roles = $"{RoleNames.InventoryOfficer},{RoleNames.Manager}")]
    public async Task<ActionResult<IReadOnlyCollection<SupplierResponse>>> GetSuppliers(
        [FromQuery] bool activeOnly = true,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var suppliers = await _supplierService.GetSuppliersAsync(activeOnly, search, cancellationToken);
        var response = suppliers
            .Select(s => new SupplierResponse(
                s.Id,
                s.Name,
                s.ContactName,
                s.ContactPhone,
                s.ContactEmail,
                s.Address,
                s.IsActive))
            .ToList();

        return Ok(response);
    }

    [HttpPost]
    [Authorize(Roles = $"{RoleNames.InventoryOfficer},{RoleNames.Manager}")]
    public async Task<ActionResult<SupplierResponse>> CreateSupplier(
        CreateSupplierRequest request,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        var supplier = await _supplierService.CreateAsync(
            actorUserId,
            request.Name,
            request.ContactName,
            request.ContactPhone,
            request.ContactEmail,
            request.Address,
            cancellationToken);
        return Ok(new SupplierResponse(
            supplier.Id,
            supplier.Name,
            supplier.ContactName,
            supplier.ContactPhone,
            supplier.ContactEmail,
            supplier.Address,
            supplier.IsActive));
    }

    [HttpPatch("{supplierId:guid}/deactivate")]
    [Authorize(Roles = $"{RoleNames.InventoryOfficer},{RoleNames.Manager}")]
    public async Task<IActionResult> DeactivateSupplier(Guid supplierId, CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        await _supplierService.DeactivateAsync(actorUserId, supplierId, cancellationToken);
        return NoContent();
    }
}
