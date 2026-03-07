using NVGInventory.Modules.Dispatching.Enums;

namespace NVGInventory.Modules.Dispatching;

public static class DispatchDocumentRules
{
    public static IReadOnlyCollection<TripDocumentType> GetRequiredDocumentTypes(DispatchingOptions options)
    {
        var required = new List<TripDocumentType> { TripDocumentType.Pod };

        if (options.RequireWaybill)
        {
            required.Add(TripDocumentType.Waybill);
        }

        if (options.RequireATW)
        {
            required.Add(TripDocumentType.Atw);
        }

        return required;
    }

    public static int GetRequiredDocumentCount(DispatchingOptions options)
    {
        return GetRequiredDocumentTypes(options).Count;
    }
}
