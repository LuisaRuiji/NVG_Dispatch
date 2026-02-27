using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NVGInventory.Contracts;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Services;
using NVGInventory.Security;

namespace NVGInventory.Controllers;

[ApiController]
[Route("api/modules")]
[Authorize]
public sealed class ModuleSettingsController : ControllerBase
{
    private readonly ModuleSettingsService _moduleSettingsService;

    public ModuleSettingsController(ModuleSettingsService moduleSettingsService)
    {
        _moduleSettingsService = moduleSettingsService;
    }

    [HttpGet]
    [Authorize(Roles = RoleNames.SuperAdmin)]
    public async Task<ActionResult<IReadOnlyCollection<ModuleSettingResponse>>> GetModules(CancellationToken cancellationToken)
    {
        var modules = await _moduleSettingsService.GetModulesAsync(cancellationToken);

        var response = modules.Select(module => new ModuleSettingResponse(
            module.ModuleKey,
            module.DisplayName,
            module.IsEnabled,
            module.CreatedAt,
            module.UpdatedAt,
            module.UpdatedByUserId,
            module.UpdatedByUsername,
            module.Notes)).ToList();

        return Ok(response);
    }

    [HttpPatch("{moduleKey}")]
    [Authorize(Roles = RoleNames.SuperAdmin)]
    public async Task<ActionResult<ModuleSettingResponse>> UpdateModule(
        string moduleKey,
        UpdateModuleSettingRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _moduleSettingsService.UpdateModuleAsync(
            moduleKey,
            request.IsEnabled,
            User.GetUserId(),
            request.Notes,
            cancellationToken);

        var response = new ModuleSettingResponse(
            updated.ModuleKey,
            updated.DisplayName,
            updated.IsEnabled,
            updated.CreatedAt,
            updated.UpdatedAt,
            updated.UpdatedByUserId,
            updated.UpdatedByUsername,
            updated.Notes);

        return Ok(response);
    }

    [HttpGet("status")]
    public async Task<ActionResult<IReadOnlyCollection<ModuleStatusResponse>>> GetModuleStatus(CancellationToken cancellationToken)
    {
        var modules = await _moduleSettingsService.GetModulesAsync(cancellationToken);

        var response = modules.Select(module => new ModuleStatusResponse(
            module.ModuleKey,
            module.DisplayName,
            module.IsEnabled,
            module.Notes)).ToList();

        return Ok(response);
    }
}
