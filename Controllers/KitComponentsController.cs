using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NVGInventory.Contracts;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Services;

namespace NVGInventory.Controllers;

[ApiController]
[Route("api/inventory/{inventoryId:guid}/components")]
[Authorize(Roles = $"{RoleNames.InventoryOfficer},{RoleNames.Manager}")]
public sealed class KitComponentsController : ControllerBase
{
    private readonly KitComponentService _kitComponentService;

    public KitComponentsController(KitComponentService kitComponentService)
    {
        _kitComponentService = kitComponentService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<KitComponentResponse>>> GetComponents(
        Guid inventoryId,
        CancellationToken cancellationToken)
    {
        var components = await _kitComponentService.GetComponentsAsync(inventoryId, cancellationToken);
        var response = components
            .Select(component => new KitComponentResponse(
                component.Id,
                component.InventoryItemId,
                component.Name,
                component.RequiredQty,
                component.IsRequired,
                component.Notes,
                component.CreatedAt))
            .ToList();

        return Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<KitComponentResponse>> CreateComponent(
        Guid inventoryId,
        CreateKitComponentRequest request,
        CancellationToken cancellationToken)
    {
        var component = await _kitComponentService.CreateComponentAsync(
            new CreateKitComponentCommand(
                inventoryId,
                request.Name,
                request.RequiredQty,
                request.IsRequired,
                request.Notes),
            cancellationToken);

        return Ok(new KitComponentResponse(
            component.Id,
            component.InventoryItemId,
            component.Name,
            component.RequiredQty,
            component.IsRequired,
            component.Notes,
            component.CreatedAt));
    }

    [HttpPut("{componentId:guid}")]
    public async Task<ActionResult<KitComponentResponse>> UpdateComponent(
        Guid inventoryId,
        Guid componentId,
        UpdateKitComponentRequest request,
        CancellationToken cancellationToken)
    {
        var component = await _kitComponentService.UpdateComponentAsync(
            new UpdateKitComponentCommand(
                inventoryId,
                componentId,
                request.Name,
                request.RequiredQty,
                request.IsRequired,
                request.Notes),
            cancellationToken);

        return Ok(new KitComponentResponse(
            component.Id,
            component.InventoryItemId,
            component.Name,
            component.RequiredQty,
            component.IsRequired,
            component.Notes,
            component.CreatedAt));
    }

    [HttpDelete("{componentId:guid}")]
    public async Task<IActionResult> DeleteComponent(
        Guid inventoryId,
        Guid componentId,
        CancellationToken cancellationToken)
    {
        await _kitComponentService.DeleteComponentAsync(inventoryId, componentId, cancellationToken);
        return NoContent();
    }

    [HttpPost("import")]
    public async Task<ActionResult<ImportKitComponentsResponse>> ImportComponents(
        Guid inventoryId,
        ImportKitComponentsRequest request,
        CancellationToken cancellationToken)
    {
        var mode = ParseMode(request.Mode);
        var result = await _kitComponentService.ImportComponentsAsync(
            new ImportKitComponentsCommand(
                inventoryId,
                mode,
                request.Lines.Select(line => new KitComponentImportLine(
                    line.Name,
                    line.RequiredQty,
                    line.IsRequired,
                    line.Notes)).ToList()),
            cancellationToken);

        return Ok(new ImportKitComponentsResponse(result.Added, result.Updated, result.Removed));
    }

    private static KitComponentImportMode ParseMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            return KitComponentImportMode.Replace;
        }

        return mode.Trim().ToUpperInvariant() switch
        {
            "REPLACE" => KitComponentImportMode.Replace,
            "MERGE" => KitComponentImportMode.Merge,
            _ => throw new NVGInventory.Domain.Exceptions.BusinessRuleViolationException(
                "Invalid import mode. Use REPLACE or MERGE.")
        };
    }
}
