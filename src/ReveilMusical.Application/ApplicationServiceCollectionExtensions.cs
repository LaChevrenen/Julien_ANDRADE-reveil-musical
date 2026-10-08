using Microsoft.Extensions.DependencyInjection;

namespace ReveilMusical.Application;

/// <summary>Clés des services enregistrés par nom (keyed services).</summary>
public static class ServiceKeys
{
    /// <summary>Catalogue de dernier recours : ne lève que sur annulation.</summary>
    public const string LocalCatalog = "local";

    /// <summary>Service de préférences brut, avant décoration par les préférences par défaut.</summary>
    public const string PreferencesSource = "source";
}

public static class ApplicationServiceCollectionExtensions
{
    /// <summary>Services sans état de la couche application : tous Transient.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddTransient<TrackSelector>();
        services.AddTransient<WakeUpMessageComposer>();
        services.AddTransient<INotificationDispatcher, FallbackNotificationDispatcher>();
        services.AddTransient<IWakeUpService, WakeUpService>();
        return services;
    }
}
