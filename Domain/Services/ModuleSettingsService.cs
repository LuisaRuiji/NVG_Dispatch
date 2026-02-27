using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Entities;
using NVGInventory.Domain.Exceptions;

namespace NVGInventory.Domain.Services;

public sealed record ModuleSettingInfo(
    string ModuleKey,
    string DisplayName,
    bool IsEnabled,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    Guid? UpdatedByUserId,
    string? UpdatedByUsername,
    string? Notes);

public sealed class ModuleSettingsService
{
    private readonly InventoryDbContext _dbContext;
    private readonly UserService _userService;
    private readonly IAuditService? _auditService;

    public ModuleSettingsService(
        InventoryDbContext dbContext,
        UserService userService,
        IAuditService? auditService = null)
    {
        _dbContext = dbContext;
        _userService = userService;
        _auditService = auditService;
    }

    public async Task<IReadOnlyCollection<ModuleSettingInfo>> GetModulesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);

        var settings = await _dbContext.ModuleSettings
            .AsNoTracking()
            .Include(setting => setting.UpdatedBy)
            .ToListAsync(cancellationToken);

        var settingsByKey = settings.ToDictionary(
            setting => ModuleRegistry.NormalizeKey(setting.ModuleKey),
            setting => setting,
            StringComparer.OrdinalIgnoreCase);

        return ModuleRegistry.All
            .Select(definition =>
            {
                if (settingsByKey.TryGetValue(definition.Key, out var setting))
                {
                    return new ModuleSettingInfo(
                        definition.Key,
                        definition.DisplayName,
                        setting.IsEnabled,
                        setting.CreatedAt,
                        setting.UpdatedAt,
                        setting.UpdatedByUserId,
                        setting.UpdatedBy?.Username,
                        setting.Notes);
                }

                return new ModuleSettingInfo(
                    definition.Key,
                    definition.DisplayName,
                    true,
                    DateTime.UtcNow,
                    null,
                    null,
                    null,
                    null);
            })
            .OrderBy(info => info.DisplayName)
            .ToList();
    }

    public async Task<ModuleSettingInfo> UpdateModuleAsync(
        string moduleKey,
        bool isEnabled,
        Guid actorUserId,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);

        var normalizedKey = ModuleRegistry.NormalizeKey(moduleKey);
        if (!ModuleRegistry.IsKnown(normalizedKey))
        {
            throw new NotFoundException("Module not found.");
        }

        await _userService.EnsureActiveUserAsync(actorUserId, cancellationToken);

        var setting = await _dbContext.ModuleSettings
            .FirstOrDefaultAsync(entry => entry.ModuleKey == normalizedKey, cancellationToken);

        var now = DateTime.UtcNow;
        if (setting is null)
        {
            setting = new ModuleSetting
            {
                ModuleKey = normalizedKey,
                IsEnabled = isEnabled,
                CreatedAt = now
            };
            _dbContext.ModuleSettings.Add(setting);
        }
        else
        {
            setting.IsEnabled = isEnabled;
        }

        setting.UpdatedByUserId = actorUserId;
        setting.UpdatedAt = now;
        setting.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

        _auditService?.AddEntry(
            actorUserId,
            AuditActions.ModuleSettingUpdated,
            EntityTypes.ModuleSetting,
            Guid.Empty,
            null,
            new { ModuleKey = normalizedKey, setting.IsEnabled, setting.Notes });

        await _dbContext.SaveChangesAsync(cancellationToken);

        var displayName = ModuleRegistry.GetDisplayName(normalizedKey);
        var updatedBy = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Id == setting.UpdatedByUserId, cancellationToken);

        return new ModuleSettingInfo(
            normalizedKey,
            displayName,
            setting.IsEnabled,
            setting.CreatedAt,
            setting.UpdatedAt,
            setting.UpdatedByUserId,
            updatedBy?.Username,
            setting.Notes);
    }

    public async Task<bool> IsModuleEnabledAsync(string moduleKey, CancellationToken cancellationToken = default)
    {
        var normalizedKey = ModuleRegistry.NormalizeKey(moduleKey);
        if (!ModuleRegistry.IsKnown(normalizedKey))
        {
            return true;
        }

        var setting = await _dbContext.ModuleSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.ModuleKey == normalizedKey, cancellationToken);

        return setting?.IsEnabled ?? true;
    }

    private async Task EnsureSeededAsync(CancellationToken cancellationToken)
    {
        var existingKeys = await _dbContext.ModuleSettings
            .AsNoTracking()
            .Select(setting => setting.ModuleKey)
            .ToListAsync(cancellationToken);

        var missingKeys = ModuleRegistry.All
            .Select(definition => definition.Key)
            .Except(existingKeys, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (missingKeys.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var key in missingKeys)
        {
            _dbContext.ModuleSettings.Add(new ModuleSetting
            {
                ModuleKey = ModuleRegistry.NormalizeKey(key),
                IsEnabled = true,
                CreatedAt = now
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
