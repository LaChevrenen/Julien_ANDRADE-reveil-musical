namespace ReveilMusical.Domain;

public enum WeatherType { Soleil, Pluie, Neige, Nuageux }

/// <summary>Morceau résolu. <paramref name="Source"/> est une étiquette neutre ("itunes", "local"...).</summary>
public sealed record Track(string Title, string Artist, string Source);

public sealed record UserPreferences(
    IReadOnlyDictionary<WeatherType, string> TrackByWeather,
    string FallbackTrack,
    ChannelId PreferredChannel);

public sealed record WakeUpRequest(string UserId, DayOfWeek Day, WeatherType Weather);

public sealed record WakeUpMessage(string Text);
