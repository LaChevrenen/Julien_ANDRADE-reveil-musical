using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReveilMusical.Application;
using ReveilMusical.Domain;
using ReveilMusical.Infrastructure;

namespace ReveilMusical.Host;

/// <summary>Seul endroit qui câble l'application : la configuration décide des implémentations.</summary>
public static class CompositionRoot
{
    public static IServiceCollection AddReveilMusical(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration);

        services.AddSingleton(BindNotificationPolicy(configuration));
        services.AddSingleton(new WakeUpDefaults(Required(configuration, "Defaults:FallbackTrack")));

        // Chaîne de fournisseurs : ordre = Music:Providers, modifiable sans recompiler.
        var providers = BindProviders(services, configuration);
        services.AddTransient<ITrackResolver>(sp => ActivatorUtilities.CreateInstance<ResilientTrackResolver>(
            sp,
            providers.Select(name => sp.GetRequiredKeyedService<IMusicCatalog>(name)).ToList(),
            sp.GetRequiredKeyedService<IMusicCatalog>(ServiceKeys.LocalCatalog)));

        services.AddTransient<IUserPreferencesProvider>(sp => ActivatorUtilities.CreateInstance<DefaultingUserPreferencesProvider>(
            sp,
            sp.GetRequiredKeyedService<IUserPreferencesProvider>(ServiceKeys.PreferencesSource)));

        return services;
    }

    private static NotificationPolicy BindNotificationPolicy(IConfiguration configuration)
    {
        var fallbackOrder = (configuration.GetSection("Notification:FallbackOrder").Get<string[]>() ?? [])
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => new ChannelId(name))
            .ToList();

        return new NotificationPolicy(fallbackOrder, new ChannelId(Required(configuration, "Notification:DefaultChannel")));
    }

    /// <summary>Échec rapide au démarrage si la configuration nomme un fournisseur non enregistré.</summary>
    private static IReadOnlyList<string> BindProviders(IServiceCollection services, IConfiguration configuration)
    {
        var known = services
            .Where(d => d.IsKeyedService && d.ServiceType == typeof(IMusicCatalog))
            .Select(d => d.ServiceKey as string)
            .OfType<string>()
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToList();

        var configured = (configuration.GetSection("Music:Providers").Get<string[]>() ?? [])
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim().ToLowerInvariant())
            .ToList();

        var unknown = configured.Except(known).ToList();
        if (unknown.Count > 0)
        {
            throw new InvalidOperationException(
                $"Music:Providers contient un fournisseur inconnu : {string.Join(", ", unknown)}. " +
                $"Noms valides : {string.Join(", ", known)}.");
        }

        return configured;
    }

    private static string Required(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Configuration manquante : {key}.")
            : value;
    }
}
