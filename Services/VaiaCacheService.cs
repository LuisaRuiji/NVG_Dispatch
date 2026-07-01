using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace NVGInventory.Services;

public interface IVaiaCacheService
{
    Task<T?> GetOrSetAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan ttl,
        CancellationToken ct = default) where T : class;

    void Invalidate(string key);

    void InvalidatePrefix(string prefix);
}

public sealed class VaiaCacheService : IVaiaCacheService
{
    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<string, byte> _keys = new(StringComparer.Ordinal);

    public VaiaCacheService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public async Task<T?> GetOrSetAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan ttl,
        CancellationToken ct = default) where T : class
    {
        if (_cache.TryGetValue<T>(key, out var cached))
        {
            return cached;
        }

        ct.ThrowIfCancellationRequested();
        var value = await factory();
        if (value is null)
        {
            return null;
        }

        _cache.Set(key, value, ttl);
        _keys[key] = 0;
        return value;
    }

    public void Invalidate(string key)
    {
        _cache.Remove(key);
        _keys.TryRemove(key, out _);
    }

    public void InvalidatePrefix(string prefix)
    {
        foreach (var key in _keys.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)).ToList())
        {
            _cache.Remove(key);
            _keys.TryRemove(key, out _);
        }
    }
}
