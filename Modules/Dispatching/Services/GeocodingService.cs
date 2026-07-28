using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NVGInventory.Modules.Dispatching.Services;

public sealed class GeocodingService : IGeocodingService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GeocodingService> _logger;
    private static readonly ConcurrentDictionary<string, GeocodingResult?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public GeocodingService(IHttpClientFactory httpClientFactory, ILogger<GeocodingService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<GeocodingResult?> GeocodeAddressAsync(string address, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(address)) return null;

        var key = address.Trim();
        if (Cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var parts = key.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return null;

        var candidates = new List<string>();
        if (parts.Length >= 3)
        {
            candidates.Add($"{parts[0]}, {parts[2]}");
        }
        if (parts.Length >= 2)
        {
            candidates.Add($"{parts[0]}, {parts[1]}");
        }
        candidates.Add($"{parts[0]}, Davao City");
        candidates.Add(parts[0]);
        candidates.Add(key);
        candidates.Add("Davao City, Philippines");

        var client = _httpClientFactory.CreateClient("GeocodingClient");
        if (client.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("NVG_Dispatch/1.0 (contact@nvglogistics.com)");
        }

        const string bbox = "&countrycodes=ph&viewbox=125.35,7.35,125.75,6.95";

        foreach (var queryStr in candidates.Distinct())
        {
            try
            {
                var url = $"https://nominatim.openstreetmap.org/search?format=json&q={Uri.EscapeDataString(queryStr)}&limit=1{bbox}";
                var response = await client.GetFromJsonAsync<List<NominatimDto>>(url, cancellationToken);

                if (response != null && response.Count > 0)
                {
                    var item = response[0];
                    if (double.TryParse(item.Lat, out var lat) && double.TryParse(item.Lon, out var lon))
                    {
                        var result = new GeocodingResult(lat, lon, item.DisplayName ?? queryStr);
                        Cache[key] = result;
                        return result;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Geocoding candidate failed for query: {Query}", queryStr);
            }
        }

        Cache[key] = null;
        return null;
    }

    private sealed record NominatimDto(
        [property: JsonPropertyName("lat")] string Lat,
        [property: JsonPropertyName("lon")] string Lon,
        [property: JsonPropertyName("display_name")] string DisplayName);
}
