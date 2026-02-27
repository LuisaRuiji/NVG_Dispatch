using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NVGInventory.Contracts;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Services;

namespace NVGInventory.Controllers;

[ApiController]
[Route("api/inventory")]
[Authorize]
public sealed class InventoryController : ControllerBase
{
    private readonly InventoryService _inventoryService;

    public InventoryController(InventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    [HttpPost]
    public async Task<ActionResult<InventoryItemResponse>> CreateItem(
        CreateInventoryItemRequest request,
        CancellationToken cancellationToken)
    {
        var item = await _inventoryService.CreateItemAsync(
            new CreateInventoryItemCommand(
                request.Name,
                request.Unit,
                request.ItemType,
                request.Quantity,
                request.ReorderLevel,
                request.Location,
                request.UnitValue,
                request.IsKit ?? false),
            cancellationToken);

        return Ok(new InventoryItemResponse(
            item.Id,
            item.Name,
            item.Unit,
            item.ItemType,
            item.IsKit,
            item.Quantity,
            item.ReorderLevel,
            item.Location,
            item.UnitValue));
    }

    [HttpPut("{inventoryId:guid}")]
    [Authorize(Roles = $"{RoleNames.InventoryOfficer},{RoleNames.Manager}")]
    public async Task<ActionResult<InventoryItemResponse>> UpdateItem(
        Guid inventoryId,
        UpdateInventoryItemRequest request,
        CancellationToken cancellationToken)
    {
        var item = await _inventoryService.UpdateItemAsync(
            inventoryId,
            new UpdateInventoryItemCommand(
                request.Name,
                request.Unit,
                request.ItemType,
                request.ReorderLevel,
                request.Location,
                request.UnitValue,
                request.IsKit),
            cancellationToken);

        return Ok(new InventoryItemResponse(
            item.Id,
            item.Name,
            item.Unit,
            item.ItemType,
            item.IsKit,
            item.Quantity,
            item.ReorderLevel,
            item.Location,
            item.UnitValue));
    }

    [HttpPatch("{inventoryId:guid}/archive")]
    [Authorize(Roles = $"{RoleNames.InventoryOfficer},{RoleNames.Manager}")]
    public async Task<IActionResult> ArchiveItem(
        Guid inventoryId,
        CancellationToken cancellationToken)
    {
        await _inventoryService.ArchiveItemAsync(inventoryId, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{inventoryId:guid}/kit")]
    [Authorize(Roles = $"{RoleNames.InventoryOfficer},{RoleNames.Manager}")]
    public async Task<IActionResult> UpdateKitFlag(
        Guid inventoryId,
        UpdateInventoryKitRequest request,
        CancellationToken cancellationToken)
    {
        await _inventoryService.SetKitFlagAsync(inventoryId, request.IsKit, cancellationToken);
        return NoContent();
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<InventoryItemResponse>>> GetItems(
        [FromQuery] string? itemType,
        [FromQuery] bool? lowStock,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        Domain.Enums.ItemType? parsedType = null;
        if (!string.IsNullOrWhiteSpace(itemType))
        {
            if (!QueryParsing.TryParseEnum(itemType, out Domain.Enums.ItemType parsed))
            {
                return BadRequest("Invalid item type.");
            }

            parsedType = parsed;
        }

        var items = await _inventoryService.GetItemsAsync(parsedType, lowStock, search, cancellationToken);
        var response = items
            .Select(item => new InventoryItemResponse(
                item.Id,
                item.Name,
                item.Unit,
                item.ItemType,
                item.IsKit,
                item.Quantity,
                item.ReorderLevel,
                item.Location,
                item.UnitValue))
            .ToList();

        return Ok(response);
    }
}
