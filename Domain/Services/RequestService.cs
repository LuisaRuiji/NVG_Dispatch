using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Entities;
using NVGInventory.Domain.Enums;
using NVGInventory.Domain.Exceptions;

namespace NVGInventory.Domain.Services;

public sealed record RequestLineInput(Guid InventoryId, decimal Quantity, string? Remarks);

public sealed record CreateRequestCommand(
    RequestType RequestType,
    Guid RequesterUserId,
    Guid? AssetId,
    string? Purpose,
    IReadOnlyCollection<RequestLineInput> Lines);

public sealed class RequestService
{
    private readonly InventoryDbContext _dbContext;
    private readonly IAuditService? _auditService;

    public RequestService(InventoryDbContext dbContext, IAuditService? auditService = null)
    {
        _dbContext = dbContext;
        _auditService = auditService;
    }

    public async Task<Request> CreateRequestAsync(
        CreateRequestCommand command,
        RequestStatus status = RequestStatus.Draft,
        CancellationToken cancellationToken = default)
    {
        if (command.Lines.Count == 0)
        {
            throw new BusinessRuleViolationException("Request must include at least one line.");
        }

        if (command.RequestType == RequestType.MaintenanceIssue && command.AssetId is null)
        {
            throw new BusinessRuleViolationException("Maintenance issue requests require an asset.");
        }

        if (command.RequestType == RequestType.AdjustmentDamageLoss && string.IsNullOrWhiteSpace(command.Purpose))
        {
            throw new BusinessRuleViolationException("Adjustment requests require a reason.");
        }

        foreach (var line in command.Lines)
        {
            if (command.RequestType == RequestType.AdjustmentDamageLoss)
            {
                if (line.Quantity == 0)
                {
                    throw new BusinessRuleViolationException("Adjustment quantity must be non-zero.");
                }
            }
            else
            {
                if (line.Quantity <= 0)
                {
                    throw new BusinessRuleViolationException("Requested quantity must be greater than zero.");
                }
            }

            if (line.Quantity % 1m != 0m)
            {
                throw new BusinessRuleViolationException("Requested quantity must be a whole number.");
            }
        }

        var inventoryIdDuplicates = command.Lines
            .GroupBy(line => line.InventoryId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();
        if (inventoryIdDuplicates.Count > 0)
        {
            throw new BusinessRuleViolationException("Duplicate inventory items are not allowed on the same request.");
        }

        var requesterExists = await _dbContext.Users
            .AnyAsync(user => user.Id == command.RequesterUserId, cancellationToken);
        if (!requesterExists)
        {
            throw new NotFoundException("Requester not found.");
        }

        if (command.AssetId is not null)
        {
            var asset = await _dbContext.Assets
                .FirstOrDefaultAsync(asset => asset.Id == command.AssetId, cancellationToken);
            if (asset is null)
            {
                throw new NotFoundException("Asset not found for request.");
            }

            if (asset.Status != AssetStatus.Active)
            {
                throw new BusinessRuleViolationException("Asset is inactive.");
            }
        }

        var inventoryIds = command.Lines.Select(line => line.InventoryId).Distinct().ToArray();
        var inventoryItems = await _dbContext.InventoryItems
            .Where(item => inventoryIds.Contains(item.Id))
            .ToListAsync(cancellationToken);

        if (inventoryItems.Count != inventoryIds.Length)
        {
            throw new NotFoundException("One or more inventory items were not found.");
        }

        if (inventoryItems.Any(item => !item.IsActive))
        {
            throw new BusinessRuleViolationException("One or more inventory items are inactive.");
        }

        EnforceItemTypeRules(command.RequestType, inventoryItems);

        var now = DateTime.UtcNow;
        var request = new Request
        {
            Id = Guid.NewGuid(),
            RequestType = command.RequestType,
            RequesterUserId = command.RequesterUserId,
            AssetId = command.AssetId,
            Purpose = command.Purpose,
            Status = status,
            CreatedAt = now,
            Lines = command.Lines.Select(line => new RequestLine
            {
                Id = Guid.NewGuid(),
                InventoryId = line.InventoryId,
                QtyRequested = line.Quantity,
                Remarks = line.Remarks,
                CreatedAt = now
            }).ToList()
        };

        _dbContext.Requests.Add(request);
        _auditService?.AddEntry(
            command.RequesterUserId,
            AuditActions.RequestCreated,
            EntityTypes.Request,
            request.Id,
            null,
            new
            {
                request.RequestType,
                request.Status,
                request.AssetId,
                LineCount = request.Lines.Count
            });
        await _dbContext.SaveChangesAsync(cancellationToken);

        return request;
    }

    private static void EnforceItemTypeRules(RequestType requestType, IReadOnlyCollection<InventoryItem> items)
    {
        if (requestType == RequestType.AdjustmentDamageLoss)
        {
            return;
        }

        if (requestType == RequestType.MaintenanceIssue)
        {
            if (items.Any(item => item.ItemType != ItemType.Consumable))
            {
                throw new BusinessRuleViolationException("Maintenance issue requests can only include consumables.");
            }
        }

        if (requestType == RequestType.Borrow)
        {
            if (items.Any(item => item.ItemType != ItemType.NonConsumable))
            {
                throw new BusinessRuleViolationException("Borrow requests can only include non-consumables.");
            }
        }
    }
}
