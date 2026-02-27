namespace NVGInventory.Domain.Constants;

public sealed record ModuleDefinition(string Key, string DisplayName, IReadOnlyCollection<string> RoutePrefixes);

public static class ModuleRegistry
{
    public const string Auth = "auth";
    public const string Users = "users";
    public const string Assets = "assets";
    public const string Inventory = "inventory";
    public const string InventoryAdjustments = "inventory-adjustments";
    public const string Loans = "loans";
    public const string PurchaseOrders = "purchase-orders";
    public const string Reports = "reports";
    public const string Requests = "requests";
    public const string Suppliers = "suppliers";
    public const string Approvals = "approvals";
    public const string Dispatching = "dispatch";

    public static readonly IReadOnlyCollection<ModuleDefinition> All =
    [
        new ModuleDefinition(Auth, "Auth", new[] { "auth" }),
        new ModuleDefinition(Users, "Users", new[] { "users" }),
        new ModuleDefinition(Assets, "Assets", new[] { "assets" }),
        new ModuleDefinition(Inventory, "Inventory", new[] { "inventory" }),
        new ModuleDefinition(InventoryAdjustments, "Inventory Adjustments", new[] { "inventory-adjustments" }),
        new ModuleDefinition(Loans, "Loans", new[] { "loans" }),
        new ModuleDefinition(PurchaseOrders, "Purchase Orders", new[] { "purchase-orders" }),
        new ModuleDefinition(Reports, "Reports", new[] { "reports" }),
        new ModuleDefinition(Requests, "Requests", new[] { "requests" }),
        new ModuleDefinition(Suppliers, "Suppliers", new[] { "suppliers" }),
        new ModuleDefinition(Approvals, "Approvals", new[] { "approvals" }),
        new ModuleDefinition(Dispatching, "Dispatching", new[] { "dispatch" })
    ];

    private static readonly Dictionary<string, string> RouteMap = BuildRouteMap();
    private static readonly Dictionary<string, ModuleDefinition> ModuleMap = BuildModuleMap();

    public static bool TryGetModuleKeyForRoute(string routePrefix, out string moduleKey)
    {
        moduleKey = string.Empty;
        if (string.IsNullOrWhiteSpace(routePrefix))
        {
            return false;
        }

        if (RouteMap.TryGetValue(NormalizeKey(routePrefix), out var found) && !string.IsNullOrWhiteSpace(found))
        {
            moduleKey = found;
            return true;
        }

        return false;
    }

    public static bool IsKnown(string moduleKey)
    {
        if (string.IsNullOrWhiteSpace(moduleKey))
        {
            return false;
        }

        return ModuleMap.ContainsKey(NormalizeKey(moduleKey));
    }

    public static string NormalizeKey(string moduleKey)
    {
        return moduleKey.Trim().ToLowerInvariant();
    }

    public static string GetDisplayName(string moduleKey)
    {
        if (string.IsNullOrWhiteSpace(moduleKey))
        {
            return string.Empty;
        }

        return ModuleMap.TryGetValue(NormalizeKey(moduleKey), out var definition)
            ? definition.DisplayName
            : moduleKey;
    }

    private static Dictionary<string, string> BuildRouteMap()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var module in All)
        {
            foreach (var route in module.RoutePrefixes)
            {
                map[NormalizeKey(route)] = module.Key;
            }
        }

        return map;
    }

    private static Dictionary<string, ModuleDefinition> BuildModuleMap()
    {
        var map = new Dictionary<string, ModuleDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var module in All)
        {
            map[NormalizeKey(module.Key)] = module;
        }

        return map;
    }
}
