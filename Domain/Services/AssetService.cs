using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Entities;
using NVGInventory.Domain.Enums;
using NVGInventory.Domain.Exceptions;

namespace NVGInventory.Domain.Services;

public sealed record CreateAssetCommand(
    string AssetCode,
    AssetType AssetType,
    string? PlateNo,
    AssetStatus Status);

public sealed class AssetService
{
    private readonly InventoryDbContext _dbContext;

    public AssetService(InventoryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Asset> CreateAssetAsync(CreateAssetCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.AssetCode))
        {
            throw new BusinessRuleViolationException("Asset code is required.");
        }

        var exists = await _dbContext.Assets.AnyAsync(
            asset => asset.AssetCode == command.AssetCode,
            cancellationToken);

        if (exists)
        {
            throw new BusinessRuleViolationException("Asset code already exists.");
        }

        var asset = new Asset
        {
            Id = Guid.NewGuid(),
            AssetCode = command.AssetCode.Trim(),
            AssetType = command.AssetType,
            PlateNo = string.IsNullOrWhiteSpace(command.PlateNo) ? null : command.PlateNo.Trim(),
            Status = command.Status,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Assets.Add(asset);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return asset;
    }

    public async Task<IReadOnlyCollection<Asset>> GetAssetsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Assets
            .AsNoTracking()
            .Where(asset => asset.Status == AssetStatus.Active)
            .OrderBy(asset => asset.AssetCode)
            .ToListAsync(cancellationToken);
    }
}
