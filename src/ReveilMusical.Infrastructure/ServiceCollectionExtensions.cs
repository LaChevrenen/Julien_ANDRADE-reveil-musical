using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ReveilMusical.Application;
using ReveilMusical.Infrastructure.Caching;
using ReveilMusical.Infrastructure.Music;
using ReveilMusical.Infrastructure.Notifications;
using ReveilMusical.Infrastructure.Preferences;

namespace ReveilMusical.Infrastructure;

/// <summary>Seul type public de l'infrastructure : enregistre adaptateurs, simulateurs et cache.</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var music = configuration.GetSection("Music").Get<MusicOptions>() ?? new MusicOptions();
        if (music.HttpTimeoutSeconds <= 0 || music.CacheTtlMinutes <= 0)
        {
            throw new InvalidOperationException(
                "Music:HttpTimeoutSeconds et Music:CacheTtlMinutes doivent être strictement positifs.");
        }

        var timeout = TimeSpan.FromSeconds(music.HttpTimeoutSeconds);
        var ttl = TimeSpan.FromMinutes(music.CacheTtlMinutes);
        var userAgent = music.MusicBrainz.UserAgent;

        // Cache : état partagé, donc Singleton ; il ne dépend que d'un autre Singleton (l'horloge).
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ICache, InMemoryCache>();

        // Fournisseurs musicaux : un HttpClient typé par adaptateur, chacun décoré par le cache.
        services.AddHttpClient<ITunesMusicCatalog>(http =>
        {
            http.BaseAddress = new Uri("https://itunes.apple.com/");
            http.Timeout = timeout;
        });
        services.AddHttpClient<MusicBrainzMusicCatalog>(http =>
        {
            MusicBrainzMusicCatalog.ConfigureClient(http, userAgent);
            http.Timeout = timeout;
        });

        services.AddKeyedTransient<IMusicCatalog>(ITunesMusicCatalog.SourceName, (sp, _) =>
            ActivatorUtilities.CreateInstance<CachedMusicCatalog>(
                sp, sp.GetRequiredService<ITunesMusicCatalog>(), ITunesMusicCatalog.SourceName, ttl));
        services.AddKeyedTransient<IMusicCatalog>(MusicBrainzMusicCatalog.SourceName, (sp, _) =>
            ActivatorUtilities.CreateInstance<CachedMusicCatalog>(
                sp, sp.GetRequiredService<MusicBrainzMusicCatalog>(), MusicBrainzMusicCatalog.SourceName, ttl));
        services.AddKeyedTransient<IMusicCatalog, LocalMusicCatalog>(ServiceKeys.LocalCatalog);

        // Canaux : ajouter WhatsApp = un client, un adaptateur et une ligne ici.
        services.AddTransient<IEmailClient>(sp => ActivatorUtilities.CreateInstance<ConsoleEmailClient>(sp, Console.Out));
        services.AddTransient<ISmsGateway, LogSmsGateway>();
        services.AddTransient<IPushService, LogPushService>();
        services.AddTransient<INotificationChannel, EmailChannel>();
        services.AddTransient<INotificationChannel, SmsChannel>();
        services.AddTransient<INotificationChannel, PushChannel>();

        services.AddKeyedTransient<IUserPreferencesProvider, InMemoryUserPreferencesProvider>(ServiceKeys.PreferencesSource);

        return services;
    }
}

internal sealed class MusicOptions
{
    public int HttpTimeoutSeconds { get; set; } = 3;

    public int CacheTtlMinutes { get; set; } = 60;

    public MusicBrainzOptions MusicBrainz { get; set; } = new();
}

internal sealed class MusicBrainzOptions
{
    public string UserAgent { get; set; } = string.Empty;
}
