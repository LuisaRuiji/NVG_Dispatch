using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace NVGInventory.Modules.Dispatching.Services;

public sealed record PlanningRecommendationSnapshot(
    string Token,
    Guid TripId,
    string TripRowVersion,
    long AvailabilityVersion,
    DateTime GeneratedAt,
    DateTime ExpiresAt,
    IReadOnlyCollection<PlanningAssignmentRecommendation> Recommendations);

public sealed class PlanningRecommendationSnapshotStore
{
    private readonly IMemoryCache _cache;
    private long _availabilityVersion = 1;

    public PlanningRecommendationSnapshotStore(IMemoryCache cache)
    {
        _cache = cache;
    }

    public long AvailabilityVersion => Interlocked.Read(ref _availabilityVersion);

    public PlanningRecommendationSnapshot Store(
        Guid tripId,
        string tripRowVersion,
        IReadOnlyCollection<PlanningAssignmentRecommendation> recommendations,
        DateTime generatedAt,
        DateTime expiresAt)
    {
        var snapshot = new PlanningRecommendationSnapshot(
            Guid.NewGuid().ToString("N"),
            tripId,
            tripRowVersion,
            AvailabilityVersion,
            generatedAt,
            expiresAt,
            recommendations);
        _cache.Set(CacheKey(snapshot.Token), snapshot, expiresAt);
        return snapshot;
    }

    public bool TryGet(string? token, out PlanningRecommendationSnapshot snapshot)
    {
        snapshot = default!;
        if (string.IsNullOrWhiteSpace(token) || !_cache.TryGetValue(CacheKey(token), out PlanningRecommendationSnapshot? cached) || cached is null)
        {
            return false;
        }

        if (cached.ExpiresAt <= DateTime.UtcNow || cached.AvailabilityVersion != AvailabilityVersion)
        {
            _cache.Remove(CacheKey(token));
            return false;
        }

        snapshot = cached;
        return true;
    }

    public long InvalidateAll() => Interlocked.Increment(ref _availabilityVersion);

    private static string CacheKey(string token) => $"planning:recommendation:{token}";
}
