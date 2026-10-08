using ReveilMusical.Domain;

namespace ReveilMusical.Host;

/// <summary>Lit <c>&lt;userId&gt; &lt;jour&gt; &lt;météo&gt;</c> depuis la ligne de commande de démonstration.</summary>
public static class WakeUpArguments
{
    public const string Usage =
        "Usage : <userId> <jour> <météo> — jour : lundi..dimanche ; météo : SOLEIL, PLUIE, NEIGE, NUAGEUX.";

    private static readonly IReadOnlyDictionary<string, DayOfWeek> Days = new Dictionary<string, DayOfWeek>(StringComparer.OrdinalIgnoreCase)
    {
        ["lundi"] = DayOfWeek.Monday,
        ["mardi"] = DayOfWeek.Tuesday,
        ["mercredi"] = DayOfWeek.Wednesday,
        ["jeudi"] = DayOfWeek.Thursday,
        ["vendredi"] = DayOfWeek.Friday,
        ["samedi"] = DayOfWeek.Saturday,
        ["dimanche"] = DayOfWeek.Sunday,
    };

    private static readonly IReadOnlyDictionary<string, WeatherType> Weathers = new Dictionary<string, WeatherType>(StringComparer.OrdinalIgnoreCase)
    {
        ["soleil"] = WeatherType.Soleil,
        ["pluie"] = WeatherType.Pluie,
        ["neige"] = WeatherType.Neige,
        ["nuageux"] = WeatherType.Nuageux,
    };

    /// <summary>Arguments absents → valeurs par défaut ; argument invalide → <c>false</c> et message d'erreur.</summary>
    public static bool TryParse(IReadOnlyList<string> args, out WakeUpRequest request, out string error)
    {
        // Les arguments « --Clé=valeur » appartiennent à la configuration, pas à la demande.
        var positional = args.Where(a => !a.StartsWith('-')).ToList();

        var userId = positional.Count > 0 ? positional[0] : "alice";
        var day = DayOfWeek.Monday;
        var weather = WeatherType.Soleil;
        request = new WakeUpRequest(userId, day, weather);

        if (positional.Count > 1 && !Days.TryGetValue(positional[1], out day))
        {
            error = $"Jour inconnu : {positional[1]}. {Usage}";
            return false;
        }

        if (positional.Count > 2 && !Weathers.TryGetValue(positional[2], out weather))
        {
            error = $"Météo inconnue : {positional[2]}. {Usage}";
            return false;
        }

        request = new WakeUpRequest(userId, day, weather);
        error = string.Empty;
        return true;
    }
}
