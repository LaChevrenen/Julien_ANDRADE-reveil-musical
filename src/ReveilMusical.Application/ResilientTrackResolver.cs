using Microsoft.Extensions.Logging;
using ReveilMusical.Domain;

namespace ReveilMusical.Application;

/// <summary>
/// Essaie chaque fournisseur dans l'ordre de configuration ; panne ou introuvable → suivant ;
/// fin de chaîne → catalogue local. Repli technique : le réveil a toujours un morceau.
/// </summary>
public sealed class ResilientTrackResolver : ITrackResolver
{
    private readonly IReadOnlyList<IMusicCatalog> _providers;
    private readonly IMusicCatalog _local;
    private readonly ILogger<ResilientTrackResolver> _logger;

    public ResilientTrackResolver(
        IReadOnlyList<IMusicCatalog> providers,
        IMusicCatalog local,
        ILogger<ResilientTrackResolver> logger)
    {
        _providers = providers;
        _local = local;
        _logger = logger;
    }

    public async Task<TrackResolution> ResolveAsync(string query, CancellationToken ct)
    {
        foreach (var provider in _providers)
        {
            var track = await TryFindAsync(provider, query, ct);
            if (track is not null)
            {
                return new TrackResolution(track, UsedLocalFallback: false);
            }
        }

        _logger.LogWarning("Aucun fournisseur musical disponible pour « {Query} » : repli sur le catalogue local.", query);

        // Le catalogue local ne peut pas échouer par contrat ; dernier filet si ce contrat était rompu.
        var local = await TryFindAsync(_local, query, ct) ?? new Track(query, string.Empty, "secours");
        return new TrackResolution(local, UsedLocalFallback: true);
    }

    private async Task<Track?> TryFindAsync(IMusicCatalog catalog, string query, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            var track = await catalog.FindAsync(query, ct);
            if (track is null)
            {
                _logger.LogWarning("{Catalog} ne trouve pas « {Query} ».", catalog.GetType().Name, query);
            }

            return track;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Un timeout HTTP arrive ici (annulation non demandée par l'appelant) : c'est une panne.
            _logger.LogWarning(ex, "{Catalog} en échec pour « {Query} ».", catalog.GetType().Name, query);
            return null;
        }
    }
}
