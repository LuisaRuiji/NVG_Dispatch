using System;
using System.Threading.Tasks;

namespace NVGInventory.Hubs;

public interface IDispatchLocationClient
{
    Task ReceiveLocationUpdate(LocationUpdateBroadcastPayload payload);
}

public sealed record LocationUpdateBroadcastPayload(
    Guid TripId,
    Guid DispatchTruckId,
    string TruckPlateNumber,
    Guid DispatchDriverId,
    Guid DriverUserId,
    decimal Latitude,
    decimal Longitude,
    decimal? AccuracyMeters,
    decimal? SpeedKph,
    decimal? Heading,
    string TripStatus,
    DateTime RecordedAt,
    DateTime ReceivedAt);
