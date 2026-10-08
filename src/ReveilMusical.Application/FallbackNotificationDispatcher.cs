using Microsoft.Extensions.Logging;
using ReveilMusical.Domain;

namespace ReveilMusical.Application;

/// <summary>Ordre de repli des canaux et canal par défaut, issus de la configuration.</summary>
public sealed record NotificationPolicy(IReadOnlyList<ChannelId> FallbackOrder, ChannelId DefaultChannel);

/// <summary>
/// Essaie le canal préféré, puis ceux de <see cref="NotificationPolicy.FallbackOrder"/> sans doublon.
/// Un canal inconnu (pas encore enregistré) est un échec comme un autre. Ne lève jamais hors annulation.
/// </summary>
public sealed class FallbackNotificationDispatcher : INotificationDispatcher
{
    private readonly IReadOnlyList<INotificationChannel> _channels;
    private readonly NotificationPolicy _policy;
    private readonly ILogger<FallbackNotificationDispatcher> _logger;

    public FallbackNotificationDispatcher(
        IEnumerable<INotificationChannel> channels,
        NotificationPolicy policy,
        ILogger<FallbackNotificationDispatcher> logger)
    {
        _channels = channels.ToList();
        _policy = policy;
        _logger = logger;
    }

    public async Task<ChannelId?> DispatchAsync(string userId, ChannelId preferred, WakeUpMessage message, CancellationToken ct)
    {
        foreach (var id in _policy.FallbackOrder.Prepend(preferred).Distinct())
        {
            ct.ThrowIfCancellationRequested();

            var channel = _channels.FirstOrDefault(c => c.Id == id);
            if (channel is null)
            {
                _logger.LogWarning("Canal « {Channel} » inconnu : essai du canal suivant.", id);
                continue;
            }

            try
            {
                await channel.SendAsync(userId, message, ct);
                return id;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Canal « {Channel} » en échec : essai du canal suivant.", id);
            }
        }

        _logger.LogCritical("Aucun canal n'a pu notifier l'utilisateur {UserId} : réveil non délivré.", userId);
        return null;
    }
}
