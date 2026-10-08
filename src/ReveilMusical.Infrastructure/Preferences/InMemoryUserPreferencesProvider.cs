using ReveilMusical.Application;
using ReveilMusical.Domain;

namespace ReveilMusical.Infrastructure.Preferences;

/// <summary>
/// Le « service interne » de l'énoncé, simulé en mémoire avec trois utilisateurs de démonstration.
/// Utilisateur inconnu → <see cref="UserNotFoundException"/>.
/// </summary>
internal sealed class InMemoryUserPreferencesProvider : IUserPreferencesProvider
{
    private static readonly IReadOnlyDictionary<string, UserPreferences> Users = new Dictionary<string, UserPreferences>(StringComparer.OrdinalIgnoreCase)
    {
        ["alice"] = new(
            new Dictionary<WeatherType, string>
            {
                [WeatherType.Soleil] = "Walking on Sunshine",
                [WeatherType.Pluie] = "Purple Rain",
                [WeatherType.Neige] = "Let It Snow",
                [WeatherType.Nuageux] = "Both Sides, Now",
            },
            "Here Comes the Sun",
            new ChannelId("email")),

        // Ne couvre ni la neige ni les nuages : son morceau de secours s'applique.
        ["bob"] = new(
            new Dictionary<WeatherType, string>
            {
                [WeatherType.Soleil] = "Mr. Blue Sky",
                [WeatherType.Pluie] = "Riders on the Storm",
            },
            "Eye of the Tiger",
            new ChannelId("sms")),

        ["chloe"] = new(
            new Dictionary<WeatherType, string>
            {
                [WeatherType.Soleil] = "L'Été indien",
                [WeatherType.Pluie] = "Il pleut sur Nantes",
                [WeatherType.Neige] = "Tombe la neige",
            },
            "Non, je ne regrette rien",
            new ChannelId("push")),
    };

    public Task<UserPreferences> GetAsync(string userId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        return Users.TryGetValue(userId.Trim(), out var preferences)
            ? Task.FromResult(preferences)
            : Task.FromException<UserPreferences>(new UserNotFoundException(userId));
    }
}
