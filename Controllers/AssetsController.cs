using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NVGInventory.Contracts;
using NVGInventory.Domain.Services;

namespace NVGInventory.Controllers;

[ApiController]
[Route("api/assets")]
[Authorize]
public sealed class AssetsController : ControllerBase
{
    private readonly AssetService _assetService;

    public AssetsController(AssetService assetService)
    {
        _assetService = assetService;
    }

    [HttpPost]
    public async Task<ActionResult<AssetResponse>> CreateAsset(CreateAssetRequest request, CancellationToken cancellationToken)
    {
        var asset = await _assetService.CreateAssetAsync(
            new CreateAssetCommand(
                request.AssetCode,
                request.AssetType,
                request.PlateNo,
                request.Status),
            cancellationToken);

        return Ok(new AssetResponse(asset.Id, asset.AssetCode, asset.AssetType, asset.Status, asset.PlateNo));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<AssetResponse>>> GetAssets(CancellationToken cancellationToken)
    {
        var assets = await _assetService.GetAssetsAsync(cancellationToken);
        var response = assets
            .Select(asset => new AssetResponse(asset.Id, asset.AssetCode, asset.AssetType, asset.Status, asset.PlateNo))
            .ToList();

        return Ok(response);
    }
}
