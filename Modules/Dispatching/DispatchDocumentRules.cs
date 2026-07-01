using NVGInventory.Modules.Dispatching.Enums;

namespace NVGInventory.Modules.Dispatching;

public static class DispatchDocumentRules
{
    public static IReadOnlyCollection<TripDocumentType> GetRequiredDocumentTypes(DispatchingOptions options)
    {
        return
        [
            TripDocumentType.Atw,
            TripDocumentType.Eir,
            TripDocumentType.GatePass,
            TripDocumentType.Dr,
            TripDocumentType.Waybill,
            TripDocumentType.Pod
        ];
    }

    public static int GetRequiredDocumentCount(DispatchingOptions options)
    {
        return GetRequiredDocumentTypes(options).Count;
    }
}
