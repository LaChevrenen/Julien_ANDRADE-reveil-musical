using System.Collections.Concurrent;
using ReveilMusical.Application;

namespace ReveilMusical.Infrastructure.Caching;

/// <summary>
/// Cache mémoire. État d'instance (enregistré en Singleton), aucun <c>static</c> ;
/// l'horloge est injectée pour ne pas dépendre du contexte d'exécution.
/// </summary>
internal sealed class InMemoryCache : ICache
{
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly TimeProvider _clock;

    public InMemoryCache(TimeProvider clock)
    {
        _clock = clock;
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (_entries.TryGetValue(key, out var entry))
        {
            if (entry.ExpiresAt > _clock.GetUtcNow())
            {
                return Task.FromResult(entry.Value is T value ? value : default);
            }

            _entries.TryRemove(key, out _);
        }

        return Task.FromResult<T?>(default);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        _entries[key] = new Entry(value, _clock.GetUtcNow() + ttl);
        return Task.CompletedTask;
    }

    private sealed record Entry(object? Value, DateTimeOffset ExpiresAt);
}
