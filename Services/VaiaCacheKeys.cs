namespace NVGInventory.Services;

public static class VaiaCacheKeys
{
    public const string DispatchKpis = "kpi:dispatch";
    public const string FinanceKpis = "kpi:finance";
    public const string SystemKpis = "kpi:system";

    public static string DriverKpis(Guid userId) => $"kpi:driver:{userId}";

    public static string CustomerKpis(Guid userId) => $"kpi:customer:{userId}";

    public const string DriverAvailability = "avail:drivers";
    public const string TruckAvailability = "avail:trucks";

    public const string PendingUnassignedTrips = "recommend:pending";

    public const string TripSummaryReport = "report:trip-summary";
    public const string DriverPerformanceReport = "report:driver-performance";
    public const string DeliveryTimeReport = "report:delivery-time";

    public static string UserRoles(Guid userId) => $"roles:{userId}";

    public static string ReportKey(string prefix, params object?[] parts)
    {
        return string.Join(
            ":",
            new[] { prefix }.Concat(parts.Select(part => part switch
            {
                null => "all",
                DateTime value => value.ToString("O"),
                DateOnly value => value.ToString("O"),
                _ => part.ToString() ?? "all"
            })));
    }
}
