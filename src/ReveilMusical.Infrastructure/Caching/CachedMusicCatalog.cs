using ReveilMusical.Application;
using ReveilMusical.Domain;

namespace ReveilMusical.Infrastructure.Caching;

/// <summary>
/// Décorateur de cache autour d'un fournisseur réseau (protège son quota).
/// Seuls les succès sont mis en cache : ni <c>null</c>, ni échec.
/// </summary>
internal sealed class CachedMusicCatalog : IMusicCatalog
{
    private readonly IMusicCatalog _inner;
    private readonly ICache _cache;
    private readonly string _cacheNamespace;
    private readonly TimeSpan _ttl;

    public CachedMusicCatalog(IMusicCatalog inner, ICache cache, string cacheNamespace, TimeSpan ttl)
    {
        _inner = inner;
        _cache = cache;
        _cacheNamespace = cacheNamespace;
        _ttl = ttl;
    }

    public async Task<Track?> FindAsync(string query, CancellationToken ct)
    {
        var key = $"{_cacheNamespace}:{query.Trim().ToLowerInvariant()}";

        var cached = await _cache.GetAsync<Track>(key, ct);
        if (cached is not null)
        {
            return cached;
        }

        var track = await _inner.FindAsync(query, ct);
        if (track is not null)
        {
            await _cache.SetAsync(key, track, _ttl, ct);
        }

        return track;
    }
}
