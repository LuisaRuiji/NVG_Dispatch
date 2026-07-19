using System;
using System.Threading;
using System.Threading.Tasks;

namespace NVGInventory.Modules.Dispatching.Services;

public sealed class TravelTimeService : ITravelTimeService
{
    private const double AverageSpeedKmH = 50.0;

    public Task<(decimal DistanceKm, int TravelMinutes)> EstimateTravelAsync(
        decimal fromLat, decimal fromLon,
        decimal toLat, decimal toLon,
        CancellationToken cancellationToken = default)
    {
        double lat1 = (double)fromLat;
        double lon1 = (double)fromLon;
        double lat2 = (double)toLat;
        double lon2 = (double)toLon;

        // Haversine formula
        const double R = 6371.0; // Earth radius in km
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0);
        var c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
        double distanceKm = R * c;

        double travelHours = distanceKm / AverageSpeedKmH;
        int travelMinutes = (int)Math.Max(1, Math.Round(travelHours * 60.0));

        return Task.FromResult(((decimal)distanceKm, travelMinutes));
    }

    private static double ToRadians(double angle) => Math.PI * angle / 180.0;
}
