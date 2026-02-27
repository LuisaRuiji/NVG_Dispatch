using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Exceptions;
using NVGInventory.Domain.Services;

namespace NVGInventory.Middleware;

public sealed class ModuleMaintenanceMiddleware
{
    private readonly RequestDelegate _next;

    public ModuleMaintenanceMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ModuleSettingsService moduleSettingsService)
    {
        if (!context.Request.Path.StartsWithSegments("/api", out var remainder))
        {
            await _next(context);
            return;
        }

        if (context.Request.Path.StartsWithSegments("/api/modules"))
        {
            await _next(context);
            return;
        }

        var route = remainder.Value?.Trim('/');
        if (string.IsNullOrWhiteSpace(route))
        {
            await _next(context);
            return;
        }

        var routePrefix = route.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(routePrefix))
        {
            await _next(context);
            return;
        }

        if (!ModuleRegistry.TryGetModuleKeyForRoute(routePrefix, out var moduleKey))
        {
            await _next(context);
            return;
        }

        var enabled = await moduleSettingsService.IsModuleEnabledAsync(moduleKey, context.RequestAborted);
        if (!enabled)
        {
            var displayName = ModuleRegistry.GetDisplayName(moduleKey);
            throw new ModuleDisabledException($"{displayName} module is under maintenance.");
        }

        await _next(context);
    }
}
