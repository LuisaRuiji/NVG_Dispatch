using System.Text.Json;
using System.Text.Json.Serialization;
using NVGInventory.Domain.Enums;
using NVGInventory.Modules.Dispatching.Enums;
using NVGInventory.Modules.ShipmentRequests.Enums;

namespace NVGInventory.Serialization;

public abstract class EnumStringConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    protected abstract IReadOnlyDictionary<TEnum, string> ToStringMap { get; }
    protected abstract IReadOnlyDictionary<string, TEnum> FromStringMap { get; }

    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Expected string for {typeof(TEnum).Name}.");
        }

        var raw = reader.GetString();
        if (raw is null)
        {
            throw new JsonException($"Missing value for {typeof(TEnum).Name}.");
        }

        if (!FromStringMap.TryGetValue(raw, out var value))
        {
            throw new JsonException($"Invalid {typeof(TEnum).Name} value '{raw}'.");
        }

        return value;
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        if (!ToStringMap.TryGetValue(value, out var raw))
        {
            throw new JsonException($"Unmapped {typeof(TEnum).Name} value '{value}'.");
        }

        writer.WriteStringValue(raw);
    }
}

public sealed class ItemTypeJsonConverter : EnumStringConverter<ItemType>
{
    private static readonly IReadOnlyDictionary<ItemType, string> Map = new Dictionary<ItemType, string>
    {
        [ItemType.Consumable] = "CONSUMABLE",
        [ItemType.NonConsumable] = "NON_CONSUMABLE"
    };

    private static readonly IReadOnlyDictionary<string, ItemType> Reverse =
        Map.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.OrdinalIgnoreCase);

    protected override IReadOnlyDictionary<ItemType, string> ToStringMap => Map;
    protected override IReadOnlyDictionary<string, ItemType> FromStringMap => Reverse;
}

public sealed class AssetTypeJsonConverter : EnumStringConverter<AssetType>
{
    private static readonly IReadOnlyDictionary<AssetType, string> Map = new Dictionary<AssetType, string>
    {
        [AssetType.Truck] = "TRUCK",
        [AssetType.Trailer] = "TRAILER"
    };

    private static readonly IReadOnlyDictionary<string, AssetType> Reverse =
        Map.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.OrdinalIgnoreCase);

    protected override IReadOnlyDictionary<AssetType, string> ToStringMap => Map;
    protected override IReadOnlyDictionary<string, AssetType> FromStringMap => Reverse;
}

public sealed class AssetStatusJsonConverter : EnumStringConverter<AssetStatus>
{
    private static readonly IReadOnlyDictionary<AssetStatus, string> Map = new Dictionary<AssetStatus, string>
    {
        [AssetStatus.Active] = "ACTIVE",
        [AssetStatus.Inactive] = "INACTIVE"
    };

    private static readonly IReadOnlyDictionary<string, AssetStatus> Reverse =
        Map.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.OrdinalIgnoreCase);

    protected override IReadOnlyDictionary<AssetStatus, string> ToStringMap => Map;
    protected override IReadOnlyDictionary<string, AssetStatus> FromStringMap => Reverse;
}

public sealed class RequestTypeJsonConverter : EnumStringConverter<RequestType>
{
    private static readonly IReadOnlyDictionary<RequestType, string> Map = new Dictionary<RequestType, string>
    {
        [RequestType.MaintenanceIssue] = "MAINTENANCE_ISSUE",
        [RequestType.Borrow] = "BORROW",
        [RequestType.AdjustmentDamageLoss] = "ADJUSTMENT_DAMAGE_LOSS"
    };

    private static readonly IReadOnlyDictionary<string, RequestType> Reverse =
        Map.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.OrdinalIgnoreCase);

    protected override IReadOnlyDictionary<RequestType, string> ToStringMap => Map;
    protected override IReadOnlyDictionary<string, RequestType> FromStringMap => Reverse;
}

public sealed class RequestStatusJsonConverter : EnumStringConverter<RequestStatus>
{
    private static readonly IReadOnlyDictionary<RequestStatus, string> Map = new Dictionary<RequestStatus, string>
    {
        [RequestStatus.Draft] = "DRAFT",
        [RequestStatus.Submitted] = "SUBMITTED",
        [RequestStatus.PendingIO] = "PENDING_IO",
        [RequestStatus.PendingManager] = "PENDING_MANAGER",
        [RequestStatus.Approved] = "APPROVED",
        [RequestStatus.Issued] = "ISSUED",
        [RequestStatus.Closed] = "CLOSED",
        [RequestStatus.Rejected] = "REJECTED"
    };

    private static readonly IReadOnlyDictionary<string, RequestStatus> Reverse =
        Map.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.OrdinalIgnoreCase);

    protected override IReadOnlyDictionary<RequestStatus, string> ToStringMap => Map;
    protected override IReadOnlyDictionary<string, RequestStatus> FromStringMap => Reverse;
}

public sealed class ApprovalStatusJsonConverter : EnumStringConverter<ApprovalStatus>
{
    private static readonly IReadOnlyDictionary<ApprovalStatus, string> Map = new Dictionary<ApprovalStatus, string>
    {
        [ApprovalStatus.Pending] = "PENDING",
        [ApprovalStatus.Approved] = "APPROVED",
        [ApprovalStatus.Rejected] = "REJECTED"
    };

    private static readonly IReadOnlyDictionary<string, ApprovalStatus> Reverse =
        Map.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.OrdinalIgnoreCase);

    protected override IReadOnlyDictionary<ApprovalStatus, string> ToStringMap => Map;
    protected override IReadOnlyDictionary<string, ApprovalStatus> FromStringMap => Reverse;
}

public sealed class ApprovalDecisionJsonConverter : EnumStringConverter<ApprovalDecision>
{
    private static readonly IReadOnlyDictionary<ApprovalDecision, string> Map = new Dictionary<ApprovalDecision, string>
    {
        [ApprovalDecision.Approve] = "APPROVE",
        [ApprovalDecision.Reject] = "REJECT"
    };

    private static readonly IReadOnlyDictionary<string, ApprovalDecision> Reverse =
        Map.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.OrdinalIgnoreCase);

    protected override IReadOnlyDictionary<ApprovalDecision, string> ToStringMap => Map;
    protected override IReadOnlyDictionary<string, ApprovalDecision> FromStringMap => Reverse;
}

public sealed class ReturnConditionJsonConverter : EnumStringConverter<ReturnCondition>
{
    private static readonly IReadOnlyDictionary<ReturnCondition, string> Map = new Dictionary<ReturnCondition, string>
    {
        [ReturnCondition.Good] = "GOOD",
        [ReturnCondition.Damaged] = "DAMAGED",
        [ReturnCondition.Lost] = "LOST"
    };

    private static readonly IReadOnlyDictionary<string, ReturnCondition> Reverse =
        Map.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.OrdinalIgnoreCase);

    protected override IReadOnlyDictionary<ReturnCondition, string> ToStringMap => Map;
    protected override IReadOnlyDictionary<string, ReturnCondition> FromStringMap => Reverse;
}

public sealed class LoanStatusJsonConverter : EnumStringConverter<LoanStatus>
{
    private static readonly IReadOnlyDictionary<LoanStatus, string> Map = new Dictionary<LoanStatus, string>
    {
        [LoanStatus.Open] = "OPEN",
        [LoanStatus.PartiallyReturned] = "PARTIALLY_RETURNED",
        [LoanStatus.Closed] = "CLOSED"
    };

    private static readonly IReadOnlyDictionary<string, LoanStatus> Reverse =
        Map.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.OrdinalIgnoreCase);

    protected override IReadOnlyDictionary<LoanStatus, string> ToStringMap => Map;
    protected override IReadOnlyDictionary<string, LoanStatus> FromStringMap => Reverse;
}

public sealed class PurchaseOrderStatusJsonConverter : EnumStringConverter<PurchaseOrderStatus>
{
    private static readonly IReadOnlyDictionary<PurchaseOrderStatus, string> Map = new Dictionary<PurchaseOrderStatus, string>
    {
        [PurchaseOrderStatus.Draft] = "DRAFT",
        [PurchaseOrderStatus.PendingManager] = "PENDING_MANAGER",
        [PurchaseOrderStatus.PendingFinance] = "PENDING_FINANCE",
        [PurchaseOrderStatus.PendingCeo] = "PENDING_CEO",
        [PurchaseOrderStatus.Approved] = "APPROVED",
        [PurchaseOrderStatus.Rejected] = "REJECTED",
        [PurchaseOrderStatus.PartiallyReceived] = "PARTIALLY_RECEIVED",
        [PurchaseOrderStatus.Closed] = "CLOSED"
    };

    private static readonly IReadOnlyDictionary<string, PurchaseOrderStatus> Reverse =
        Map.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.OrdinalIgnoreCase);

    protected override IReadOnlyDictionary<PurchaseOrderStatus, string> ToStringMap => Map;
    protected override IReadOnlyDictionary<string, PurchaseOrderStatus> FromStringMap => Reverse;
}

public sealed class TripStatusJsonConverter : EnumStringConverter<TripStatus>
{
    private static readonly IReadOnlyDictionary<TripStatus, string> Map = new Dictionary<TripStatus, string>
    {
        [TripStatus.Draft] = "DRAFT",
        [TripStatus.Dispatched] = "DISPATCHED",
        [TripStatus.EnroutePickup] = "ENROUTE_PICKUP",
        [TripStatus.AtPickup] = "AT_PICKUP",
        [TripStatus.Loaded] = "LOADED",
        [TripStatus.EnrouteDropoff] = "ENROUTE_DROPOFF",
        [TripStatus.AtDropoff] = "AT_DROPOFF",
        [TripStatus.Delivered] = "DELIVERED",
        [TripStatus.Closed] = "CLOSED",
        [TripStatus.Cancelled] = "CANCELLED",
        [TripStatus.OnHold] = "ON_HOLD",
        [TripStatus.FailedAttempt] = "FAILED_ATTEMPT"
    };

    private static readonly IReadOnlyDictionary<string, TripStatus> Reverse =
        Map.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.OrdinalIgnoreCase);

    protected override IReadOnlyDictionary<TripStatus, string> ToStringMap => Map;
    protected override IReadOnlyDictionary<string, TripStatus> FromStringMap => Reverse;
}

public sealed class TripStopTypeJsonConverter : EnumStringConverter<TripStopType>
{
    private static readonly IReadOnlyDictionary<TripStopType, string> Map = new Dictionary<TripStopType, string>
    {
        [TripStopType.Pickup] = "PICKUP",
        [TripStopType.Dropoff] = "DROPOFF"
    };

    private static readonly IReadOnlyDictionary<string, TripStopType> Reverse =
        Map.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.OrdinalIgnoreCase);

    protected override IReadOnlyDictionary<TripStopType, string> ToStringMap => Map;
    protected override IReadOnlyDictionary<string, TripStopType> FromStringMap => Reverse;
}

public sealed class TripDocumentTypeJsonConverter : EnumStringConverter<TripDocumentType>
{
    private static readonly IReadOnlyDictionary<TripDocumentType, string> Map = new Dictionary<TripDocumentType, string>
    {
        [TripDocumentType.Waybill] = "WAYBILL",
        [TripDocumentType.Pod] = "POD",
        [TripDocumentType.Atw] = "ATW",
        [TripDocumentType.Eir] = "EIR",
        [TripDocumentType.GatePass] = "GATE_PASS",
        [TripDocumentType.Dr] = "DR"
    };

    private static readonly IReadOnlyDictionary<string, TripDocumentType> Reverse =
        Map.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.OrdinalIgnoreCase);

    protected override IReadOnlyDictionary<TripDocumentType, string> ToStringMap => Map;
    protected override IReadOnlyDictionary<string, TripDocumentType> FromStringMap => Reverse;
}

public sealed class TripDocumentStateJsonConverter : EnumStringConverter<TripDocumentState>
{
    private static readonly IReadOnlyDictionary<TripDocumentState, string> Map = new Dictionary<TripDocumentState, string>
    {
        [TripDocumentState.Missing] = "MISSING",
        [TripDocumentState.Uploaded] = "UPLOADED",
        [TripDocumentState.Verified] = "VERIFIED",
        [TripDocumentState.Rejected] = "REJECTED"
    };

    private static readonly IReadOnlyDictionary<string, TripDocumentState> Reverse =
        Map.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.OrdinalIgnoreCase);

    protected override IReadOnlyDictionary<TripDocumentState, string> ToStringMap => Map;
    protected override IReadOnlyDictionary<string, TripDocumentState> FromStringMap => Reverse;
}

public sealed class ShipmentRequestStatusJsonConverter : EnumStringConverter<ShipmentRequestStatus>
{
    private static readonly IReadOnlyDictionary<ShipmentRequestStatus, string> Map =
        new Dictionary<ShipmentRequestStatus, string>
        {
            [ShipmentRequestStatus.Draft] = "DRAFT",
            [ShipmentRequestStatus.Submitted] = "SUBMITTED",
            [ShipmentRequestStatus.Approved] = "APPROVED",
            [ShipmentRequestStatus.Rejected] = "REJECTED",
            [ShipmentRequestStatus.ConvertedToTrip] = "CONVERTED_TO_TRIP"
        };

    private static readonly IReadOnlyDictionary<string, ShipmentRequestStatus> Reverse =
        Map.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.OrdinalIgnoreCase);

    protected override IReadOnlyDictionary<ShipmentRequestStatus, string> ToStringMap => Map;
    protected override IReadOnlyDictionary<string, ShipmentRequestStatus> FromStringMap => Reverse;
}

public sealed class ShipmentRequestDocumentTypeJsonConverter : EnumStringConverter<ShipmentRequestDocumentType>
{
    private static readonly IReadOnlyDictionary<ShipmentRequestDocumentType, string> Map =
        new Dictionary<ShipmentRequestDocumentType, string>
        {
            [ShipmentRequestDocumentType.Atw] = "ATW",
            [ShipmentRequestDocumentType.Invoice] = "INVOICE",
            [ShipmentRequestDocumentType.CargoManifest] = "CARGO_MANIFEST",
            [ShipmentRequestDocumentType.DeliveryInstructions] = "DELIVERY_INSTRUCTIONS",
            [ShipmentRequestDocumentType.Other] = "OTHER"
        };

    private static readonly IReadOnlyDictionary<string, ShipmentRequestDocumentType> Reverse =
        Map.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.OrdinalIgnoreCase);

    protected override IReadOnlyDictionary<ShipmentRequestDocumentType, string> ToStringMap => Map;
    protected override IReadOnlyDictionary<string, ShipmentRequestDocumentType> FromStringMap => Reverse;
}

public sealed class ContainerSizeJsonConverter : EnumStringConverter<ContainerSize>
{
    private static readonly IReadOnlyDictionary<ContainerSize, string> Map =
        new Dictionary<ContainerSize, string>
        {
            [ContainerSize.TwentyFt] = "TWENTY_FT",
            [ContainerSize.FortyFt] = "FORTY_FT",
            [ContainerSize.FortyHC] = "FORTY_HC"
        };

    private static readonly IReadOnlyDictionary<string, ContainerSize> Reverse =
        Map.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.OrdinalIgnoreCase);

    protected override IReadOnlyDictionary<ContainerSize, string> ToStringMap => Map;
    protected override IReadOnlyDictionary<string, ContainerSize> FromStringMap => Reverse;
}

public sealed class TripTypeJsonConverter : EnumStringConverter<TripType>
{
    private static readonly IReadOnlyDictionary<TripType, string> Map =
        new Dictionary<TripType, string>
        {
            [TripType.PortPickup] = "PORT_PICKUP",
            [TripType.PortDropoff] = "PORT_DROPOFF",
            [TripType.YardTransfer] = "YARD_TRANSFER",
            [TripType.LongHaul] = "LONG_HAUL"
        };

    private static readonly IReadOnlyDictionary<string, TripType> Reverse =
        Map.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.OrdinalIgnoreCase);

    protected override IReadOnlyDictionary<TripType, string> ToStringMap => Map;
    protected override IReadOnlyDictionary<string, TripType> FromStringMap => Reverse;
}
