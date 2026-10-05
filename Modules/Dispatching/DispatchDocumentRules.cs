using NVGInventory.Modules.Dispatching.Enums;

namespace NVGInventory.Modules.Dispatching;

public sealed record DispatchRequiredDocument(
    string DocumentCode,
    string Scope,
    DocumentDirection Direction = DocumentDirection.NotApplicable,
    string? AlternativeGroup = null,
    string? Notes = null);

public static class DispatchDocumentRules
{
    public const string ExportEmptyPickup = "EXPORT_EMPTY_PICKUP";
    public const string ExportLadenToTerminal = "EXPORT_LADEN_TO_TERMINAL";
    public const string ImportLadenDelivery = "IMPORT_LADEN_DELIVERY";
    public const string EmptyReturn = "EMPTY_RETURN";

    public static string NormalizeTripType(string? tripType) => tripType?.Trim().ToUpperInvariant().Replace('-', '_').Replace(' ', '_') switch
    {
        "EXPORTEMPTYPICKUP" or "EXPORT_EMPTY_PICKUP" => ExportEmptyPickup,
        "EXPORTLADENTOTERMINAL" or "EXPORT_LADEN_TO_TERMINAL" => ExportLadenToTerminal,
        "IMPORTLADENDELIVERY" or "IMPORT_LADEN_DELIVERY" => ImportLadenDelivery,
        "EMPTYRETURN" or "EMPTY_RETURN" => EmptyReturn,
        "PORTPICKUP" or "PORT_PICKUP" => "PORT_PICKUP",
        "PORTDROPOFF" or "PORT_DROPOFF" => "PORT_DROPOFF",
        "YARDTRANSFER" or "YARD_TRANSFER" => "YARD_TRANSFER",
        "LONGHAUL" or "LONG_HAUL" => "LONG_HAUL",
        var value when !string.IsNullOrWhiteSpace(value) => value,
        _ => "UNSPECIFIED"
    };

    public static IReadOnlyCollection<DispatchRequiredDocument> GetDefaults(
        string? tripType,
        DispatchDocumentMilestone milestone)
    {
        var normalized = NormalizeTripType(tripType);
        if (milestone == DispatchDocumentMilestone.PreDispatch)
        {
            return normalized switch
            {
                ExportEmptyPickup =>
                [
                    new("BOOKING_REFERENCE", "BOOKING"),
                    new("ATW", "SHIPMENT_OR_TRIP", AlternativeGroup: "CARRIER_RELEASE"),
                    new("RELEASE_CONFIRMATION", "SHIPMENT", AlternativeGroup: "CARRIER_RELEASE")
                ],
                ExportLadenToTerminal =>
                [
                    new("BOOKING_REFERENCE", "BOOKING", AlternativeGroup: "BOOKING_RELEASE"),
                    new("BOOKING_CONFIRMATION", "SHIPMENT", AlternativeGroup: "BOOKING_RELEASE"),
                    new("RELEASE_CONFIRMATION", "SHIPMENT", AlternativeGroup: "BOOKING_RELEASE")
                ],
                ImportLadenDelivery =>
                [
                    new("DELIVERY_ORDER", "SHIPMENT", AlternativeGroup: "IMPORT_RELEASE"),
                    new("CRO", "SHIPMENT", AlternativeGroup: "IMPORT_RELEASE"),
                    new("WEB_CRO", "SHIPMENT", AlternativeGroup: "IMPORT_RELEASE"),
                    new("RELEASE_CONFIRMATION", "SHIPMENT", AlternativeGroup: "IMPORT_RELEASE")
                ],
                EmptyReturn =>
                [
                    new("RETURN_DEPOT_AUTHORIZATION", "SHIPMENT", AlternativeGroup: "RETURN_AUTH"),
                    new("RETURN_INSTRUCTION", "SHIPMENT", AlternativeGroup: "RETURN_AUTH")
                ],
                _ => [new("ATW", "SHIPMENT_OR_TRIP")]
            };
        }

        return normalized switch
        {
            ExportEmptyPickup =>
            [
                new("EIR", "TRIP", DocumentDirection.GateOut),
                new("DTR", "TRIP"),
                new("CONTAINER_INSPECTION", "INSPECTION"),
                new("DR", "TRIP", AlternativeGroup: "DELIVERY_PROOF"),
                new("POD", "TRIP", AlternativeGroup: "DELIVERY_PROOF")
            ],
            ExportLadenToTerminal =>
            [
                new("EIR", "TRIP", DocumentDirection.GateIn),
                new("DTR", "TRIP"),
                new("GATE_EVIDENCE", "TRIP")
            ],
            ImportLadenDelivery =>
            [
                new("EIR", "TRIP", DocumentDirection.GateOut),
                new("DTR", "TRIP"),
                new("DR", "TRIP", AlternativeGroup: "DELIVERY_PROOF"),
                new("POD", "TRIP", AlternativeGroup: "DELIVERY_PROOF")
            ],
            EmptyReturn =>
            [
                new("EIR", "TRIP", DocumentDirection.GateIn),
                new("DTR", "TRIP"),
                new("RETURN_EVIDENCE", "TRIP")
            ],
            _ =>
            [
                new("EIR", "TRIP"),
                new("DTR", "TRIP"),
                new("DR", "TRIP", AlternativeGroup: "DELIVERY_PROOF"),
                new("POD", "TRIP", AlternativeGroup: "DELIVERY_PROOF")
            ]
        };
    }

    // Compatibility helpers retained for existing queue/report consumers.
    public static IReadOnlyCollection<TripDocumentType> GetRequiredDocumentTypes(DispatchingOptions options) =>
        [TripDocumentType.Eir, TripDocumentType.Dtr, TripDocumentType.Dr, TripDocumentType.Pod];

    public static int GetRequiredDocumentCount(DispatchingOptions options) => GetRequiredDocumentTypes(options).Count;
}
