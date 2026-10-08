using ReveilMusical.Domain;

namespace ReveilMusical.Application;

/// <summary>Service interne de préférences : un fournisseur comme un autre, il peut lever.</summary>
public interface IUserPreferencesProvider
{
    Task<UserPreferences> GetAsync(string userId, CancellationToken ct);
}

/// <summary>Source de morceaux. Peut renvoyer <c>null</c> (introuvable) ou lever (panne).</summary>
public interface IMusicCatalog
{
    Task<Track?> FindAsync(string query, CancellationToken ct);
}

/// <summary>Garantit un morceau : jamais <c>null</c>, jamais d'exception (hors annulation).</summary>
public interface ITrackResolver
{
    Task<TrackResolution> ResolveAsync(string query, CancellationToken ct);
}

public sealed record TrackResolution(Track Track, bool UsedLocalFallback);

/// <summary>Canal de notification. Lève en cas d'échec d'envoi.</summary>
public interface INotificationChannel
{
    ChannelId Id { get; }

    Task SendAsync(string userId, WakeUpMessage message, CancellationToken ct);
}

/// <summary>Renvoie le canal réellement utilisé, ou <c>null</c> si tous ont échoué. Ne lève jamais (hors annulation).</summary>
public interface INotificationDispatcher
{
    Task<ChannelId?> DispatchAsync(string userId, ChannelId preferred, WakeUpMessage message, CancellationToken ct);
}

/// <summary>Cache asynchrone : une implémentation distribuée peut remplacer la mémoire sans toucher au reste.</summary>
public interface ICache
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct);

    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct);
}

public interface IWakeUpService
{
    Task<WakeUpResult> SendWakeUpAsync(WakeUpRequest request, CancellationToken ct = default);
}

public sealed record WakeUpResult(Track Track, ChannelId? ChannelUsed, bool Degraded);
