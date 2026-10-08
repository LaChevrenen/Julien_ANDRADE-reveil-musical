using ReveilMusical.Domain;

namespace ReveilMusical.Host;

/// <summary>Lit <c>&lt;userId&gt; &lt;jour&gt; &lt;météo&gt;</c> depuis la ligne de commande de démonstration.</summary>
public static class WakeUpArguments
{
    public const string Usage =
        "Usage : <userId> <jour> <météo> — jour : lundi..dimanche ; météo : SOLEIL, PLUIE, NEIGE, NUAGEUX.";

    /// <summary>Arguments absents → valeurs par défaut ; argument invalide → <c>false</c> et message d'erreur.</summary>
    public static bool TryParse(IReadOnlyList<string> args, out WakeUpRequest request, out string error)
    {
        // Les arguments « --Clé=valeur » appartiennent à la configuration, pas à la demande.
        var positional = args.Where(a => !a.StartsWith('-')).ToList();

        var userId = positional.Count > 0 ? positional[0] : "alice";
        var day = positional.Count > 1 ? ParseDay(positional[1]) : DayOfWeek.Monday;
        var weather = positional.Count > 2 ? ParseWeather(positional[2]) : WeatherType.Soleil;
        request = new WakeUpRequest(userId, day ?? DayOfWeek.Monday, weather ?? WeatherType.Soleil);

        if (day is null)
        {
            error = $"Jour inconnu : {positional[1]}. {Usage}";
            return false;
        }

        if (weather is null)
        {
            error = $"Météo inconnue : {positional[2]}. {Usage}";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static DayOfWeek? ParseDay(string text) => text.Trim().ToLowerInvariant() switch
    {
        "lundi" => DayOfWeek.Monday,
        "mardi" => DayOfWeek.Tuesday,
        "mercredi" => DayOfWeek.Wednesday,
        "jeudi" => DayOfWeek.Thursday,
        "vendredi" => DayOfWeek.Friday,
        "samedi" => DayOfWeek.Saturday,
        "dimanche" => DayOfWeek.Sunday,
        _ => null,
    };

    private static WeatherType? ParseWeather(string text) => text.Trim().ToLowerInvariant() switch
    {
        "soleil" => WeatherType.Soleil,
        "pluie" => WeatherType.Pluie,
        "neige" => WeatherType.Neige,
        "nuageux" => WeatherType.Nuageux,
        _ => null,
    };
}
