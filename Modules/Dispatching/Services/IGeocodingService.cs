using System.Threading;
using System.Threading.Tasks;

namespace NVGInventory.Modules.Dispatching.Services;

public sealed record GeocodingResult(double Latitude, double Longitude, string DisplayName);

public interface IGeocodingService
{
    Task<GeocodingResult?> GeocodeAddressAsync(string address, CancellationToken cancellationToken = default);
}
