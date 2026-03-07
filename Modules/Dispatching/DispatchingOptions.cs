namespace NVGInventory.Modules.Dispatching;

public sealed class DispatchingOptions
{
    public const string SectionName = "Dispatching";

    public bool DocVerificationEnabled { get; set; } = true;
    public bool RequireWaybill { get; set; } = true;
    public bool RequireATW { get; set; } = false;
    public bool AllowPodPendingOverride { get; set; } = true;
    public bool AllowDriverOnHold { get; set; } = true;
}
