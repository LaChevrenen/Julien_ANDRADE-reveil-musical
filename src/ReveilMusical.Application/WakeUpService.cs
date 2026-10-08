using Microsoft.Extensions.Logging;
using ReveilMusical.Domain;

namespace ReveilMusical.Application;

/// <summary>
/// Facade du cas d'usage : préférences → sélection → résolution → message → envoi.
/// Ne connaît aucun fournisseur ni canal. Ne lève pas pour une panne externe :
/// chaque dépendance injectée porte son propre mode dégradé ; seule l'annulation est propagée.
/// </summary>
public sealed class WakeUpService : IWakeUpService
{
    private readonly IUserPreferencesProvider _preferences;
    private readonly TrackSelector _selector;
    private readonly ITrackResolver _resolver;
    private readonly WakeUpMessageComposer _composer;
    private readonly INotificationDispatcher _dispatcher;
    private readonly ILogger<WakeUpService> _logger;

    public WakeUpService(
        IUserPreferencesProvider preferences,
        TrackSelector selector,
        ITrackResolver resolver,
        WakeUpMessageComposer composer,
        INotificationDispatcher dispatcher,
        ILogger<WakeUpService> logger)
    {
        _preferences = preferences;
        _selector = selector;
        _resolver = resolver;
        _composer = composer;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public async Task<WakeUpResult> SendWakeUpAsync(WakeUpRequest request, CancellationToken ct = default)
    {
        var preferences = await _preferences.GetAsync(request.UserId, ct);
        var wantedTitle = _selector.Select(preferences, request.Weather);
        var resolution = await _resolver.ResolveAsync(wantedTitle, ct);
        var message = _composer.Compose(request.Day, request.Weather, resolution.Track);

        var channelUsed = await _dispatcher.DispatchAsync(request.UserId, preferences.PreferredChannel, message, ct);

        var degraded = resolution.UsedLocalFallback || channelUsed != preferences.PreferredChannel;
        if (degraded)
        {
            _logger.LogWarning(
                "Réveil de {UserId} en mode dégradé (repli local : {UsedLocalFallback}, canal : {Channel}).",
                request.UserId,
                resolution.UsedLocalFallback,
                channelUsed?.Value ?? "aucun");
        }

        return new WakeUpResult(resolution.Track, channelUsed, degraded);
    }
}
