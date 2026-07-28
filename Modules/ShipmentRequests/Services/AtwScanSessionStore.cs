using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;

namespace NVGInventory.Modules.ShipmentRequests.Services;

public sealed record AtwScanSession(Guid UserId, string FileHash, AtwExtractionResult Analysis);

public interface IAtwScanSessionStore
{
    Guid Create(Guid userId, string fileHash, AtwExtractionResult analysis);
    bool TryTake(Guid sessionId, Guid userId, string fileHash, out AtwExtractionResult analysis);
}

public sealed class AtwScanSessionStore : IAtwScanSessionStore
{
    private readonly IMemoryCache _cache;
    public AtwScanSessionStore(IMemoryCache cache) => _cache = cache;

    public Guid Create(Guid userId, string fileHash, AtwExtractionResult analysis)
    {
        var id = Guid.NewGuid();
        _cache.Set(id, new AtwScanSession(userId, fileHash, analysis), TimeSpan.FromMinutes(15));
        return id;
    }

    public bool TryTake(Guid sessionId, Guid userId, string fileHash, out AtwExtractionResult analysis)
    {
        analysis = default!;
        if (!_cache.TryGetValue(sessionId, out AtwScanSession? session) ||
            session is null || session.UserId != userId ||
            !CryptographicOperations.FixedTimeEquals(Convert.FromHexString(session.FileHash), Convert.FromHexString(fileHash)))
        {
            return false;
        }
        _cache.Remove(sessionId);
        analysis = session.Analysis;
        return true;
    }
}
