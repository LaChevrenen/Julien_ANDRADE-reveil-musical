using System.Text.Json;
using System.Text.Json.Serialization;
using ReveilMusical.Application;
using ReveilMusical.Domain;

namespace ReveilMusical.Infrastructure.Music;

/// <summary>
/// Adaptateur MusicBrainz. Même contrat que les autres fournisseurs :
/// introuvable → <c>null</c> ; HTTP ≠ 2xx ou timeout → lève.
/// </summary>
internal sealed class MusicBrainzMusicCatalog : IMusicCatalog
{
    public const string SourceName = "musicbrainz";

    private readonly HttpClient _http;

    public MusicBrainzMusicCatalog(HttpClient http)
    {
        _http = http;
    }

    /// <summary>
    /// MusicBrainz rejette les requêtes sans User-Agent identifiable (application + contact).
    /// Appelé par la configuration du HttpClient typé.
    /// </summary>
    public static void ConfigureClient(HttpClient http, string userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            throw new InvalidOperationException(
                "Music:MusicBrainz:UserAgent est obligatoire (nom d'application + contact).");
        }

        http.BaseAddress = new Uri("https://musicbrainz.org/");
        http.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
    }

    public async Task<Track?> FindAsync(string query, CancellationToken ct)
    {
        var url = $"ws/2/recording?query={Uri.EscapeDataString(query)}&fmt=json&limit=5";

        using var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        var payload = JsonSerializer.Deserialize<MusicBrainzSearchResponse>(body);

        var hit = payload?.Recordings?.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r.Title));
        if (hit is null)
        {
            return null;
        }

        var artists = (hit.ArtistCredit ?? [])
            .Select(a => a.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name));

        return new Track(hit.Title!, string.Join(", ", artists), SourceName);
    }
}

internal sealed class MusicBrainzSearchResponse
{
    [JsonPropertyName("recordings")]
    public List<MusicBrainzRecording>? Recordings { get; set; }
}

internal sealed class MusicBrainzRecording
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("artist-credit")]
    public List<MusicBrainzArtistCredit>? ArtistCredit { get; set; }
}

internal sealed class MusicBrainzArtistCredit
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}
