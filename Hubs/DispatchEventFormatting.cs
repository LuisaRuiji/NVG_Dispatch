using DispatchTripStatus = NVGInventory.Modules.Dispatching.Enums.TripStatus;
using NVGInventory.Modules.Dispatching.Enums;

namespace NVGInventory.Hubs;

public static class DispatchEventFormatting
{
    public static string TripStatus(DispatchTripStatus status)
    {
        return status switch
        {
            DispatchTripStatus.Draft => "DRAFT",
            DispatchTripStatus.Dispatched => "DISPATCHED",
            DispatchTripStatus.EnroutePickup => "ENROUTE_PICKUP",
            DispatchTripStatus.AtPickup => "AT_PICKUP",
            DispatchTripStatus.Loaded => "LOADED",
            DispatchTripStatus.EnrouteDropoff => "ENROUTE_DROPOFF",
            DispatchTripStatus.AtDropoff => "AT_DROPOFF",
            DispatchTripStatus.Delivered => "DELIVERED",
            DispatchTripStatus.Closed => "CLOSED",
            DispatchTripStatus.Cancelled => "CANCELLED",
            DispatchTripStatus.OnHold => "ON_HOLD",
            DispatchTripStatus.FailedAttempt => "FAILED_ATTEMPT",
            _ => status.ToString()
        };
    }

    public static string DocumentType(TripDocumentType type)
    {
        return type switch
        {
            TripDocumentType.Waybill => "WAYBILL",
            TripDocumentType.Pod => "POD",
            TripDocumentType.Atw => "ATW",
            TripDocumentType.Eir => "EIR",
            TripDocumentType.GatePass => "GATE_PASS",
            TripDocumentType.Dr => "DR",
            _ => type.ToString()
        };
    }
}
