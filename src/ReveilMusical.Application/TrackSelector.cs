using ReveilMusical.Domain;

namespace ReveilMusical.Application;

/// <summary>
/// Choisit le titre voulu par l'utilisateur pour la météo du jour.
/// Repli fonctionnel : météo non couverte → morceau de secours de l'utilisateur.
/// </summary>
public sealed class TrackSelector
{
    public string Select(UserPreferences preferences, WeatherType weather)
    {
        return preferences.TrackByWeather.TryGetValue(weather, out var title) && !string.IsNullOrWhiteSpace(title)
            ? title
            : preferences.FallbackTrack;
    }
}
