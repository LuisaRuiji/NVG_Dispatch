using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NVGInventory.Contracts;
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
                request.UnitValue),
            cancellationToken);

        return Ok(new InventoryItemResponse(
            item.Id,
            item.Name,
            item.Unit,
            item.ItemType,
            item.Quantity,
            item.ReorderLevel,
            item.Location,
            item.UnitValue));
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
                item.Quantity,
                item.ReorderLevel,
                item.Location,
                item.UnitValue))
            .ToList();

        return Ok(response);
    }
}
