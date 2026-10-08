using ReveilMusical.Domain;

namespace ReveilMusical.Application;

/// <summary>
/// Compose le texte du réveil à partir du jour et de la météo.
/// Dictionnaires explicites : aucun recours à la culture du contexte d'exécution.
/// </summary>
public sealed class WakeUpMessageComposer
{
    private readonly IReadOnlyDictionary<DayOfWeek, string> _days = new Dictionary<DayOfWeek, string>
    {
        [DayOfWeek.Monday] = "lundi",
        [DayOfWeek.Tuesday] = "mardi",
        [DayOfWeek.Wednesday] = "mercredi",
        [DayOfWeek.Thursday] = "jeudi",
        [DayOfWeek.Friday] = "vendredi",
        [DayOfWeek.Saturday] = "samedi",
        [DayOfWeek.Sunday] = "dimanche",
    };

    private readonly IReadOnlyDictionary<WeatherType, string> _weathers = new Dictionary<WeatherType, string>
    {
        [WeatherType.Soleil] = "il fait soleil",
        [WeatherType.Pluie] = "il pleut",
        [WeatherType.Neige] = "il neige",
        [WeatherType.Nuageux] = "le ciel est nuageux",
    };

    public WakeUpMessage Compose(DayOfWeek day, WeatherType weather, Track track)
    {
        var dayText = _days.TryGetValue(day, out var d) ? $"Nous sommes {d}" : "C'est l'heure";
        var weatherText = _weathers.TryGetValue(weather, out var w) ? $" et {w}" : string.Empty;
        var artistText = string.IsNullOrWhiteSpace(track.Artist) ? string.Empty : $" de {track.Artist}";

        return new WakeUpMessage(
            $"Bonjour ! {dayText}{weatherText}. Votre morceau du réveil : « {track.Title} »{artistText}.");
    }
}
