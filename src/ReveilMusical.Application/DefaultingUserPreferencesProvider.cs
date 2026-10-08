using Microsoft.Extensions.Logging;
using ReveilMusical.Domain;

namespace ReveilMusical.Application;

/// <summary>Levée par un service de préférences qui ne connaît pas l'utilisateur.</summary>
public sealed class UserNotFoundException : Exception
{
    public UserNotFoundException(string userId)
        : base($"Utilisateur inconnu : {userId}")
    {
        UserId = userId;
    }

    public string UserId { get; }
}

/// <summary>Valeurs de secours issues de la configuration.</summary>
public sealed record WakeUpDefaults(string FallbackTrack);

/// <summary>
/// Décorateur : si le service de préférences lève ou ne connaît pas l'utilisateur,
/// renvoie des préférences par défaut. Le réveil part quand même.
/// </summary>
public sealed class DefaultingUserPreferencesProvider : IUserPreferencesProvider
{
    private readonly IUserPreferencesProvider _inner;
    private readonly UserPreferences _defaults;
    private readonly ILogger<DefaultingUserPreferencesProvider> _logger;

    public DefaultingUserPreferencesProvider(
        IUserPreferencesProvider inner,
        WakeUpDefaults defaults,
        NotificationPolicy policy,
        ILogger<DefaultingUserPreferencesProvider> logger)
    {
        _inner = inner;
        _defaults = new UserPreferences(
            new Dictionary<WeatherType, string>(),
            defaults.FallbackTrack,
            policy.DefaultChannel);
        _logger = logger;
    }

    public async Task<UserPreferences> GetAsync(string userId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            return await _inner.GetAsync(userId, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Préférences indisponibles pour {UserId} : préférences par défaut.", userId);
            return _defaults;
        }
    }
}
