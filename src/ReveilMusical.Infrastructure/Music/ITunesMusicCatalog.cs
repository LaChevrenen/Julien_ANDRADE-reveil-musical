using System.Text.Json;
using System.Text.Json.Serialization;
using ReveilMusical.Application;
using ReveilMusical.Domain;

namespace ReveilMusical.Infrastructure.Music;

/// <summary>
/// Adaptateur iTunes Search. Les champs propres à iTunes restent dans ce fichier.
/// Introuvable → <c>null</c> ; HTTP ≠ 2xx ou timeout → lève (au resolver de basculer).
/// </summary>
internal sealed class ITunesMusicCatalog : IMusicCatalog
{
    public const string SourceName = "itunes";

    private readonly HttpClient _http;

    public ITunesMusicCatalog(HttpClient http)
    {
        _http = http;
    }

    public async Task<Track?> FindAsync(string query, CancellationToken ct)
    {
        var url = $"search?term={Uri.EscapeDataString(query)}&media=music&limit=5";

        using var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        // iTunes répond souvent en text/javascript : on lit la chaîne puis on désérialise.
        var body = await response.Content.ReadAsStringAsync(ct);
        var payload = JsonSerializer.Deserialize<ITunesSearchResponse>(body);

        var hit = payload?.Results?.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r.TrackName));
        return hit is null
            ? null
            : new Track(hit.TrackName!, hit.ArtistName ?? string.Empty, SourceName);
    }
}

internal sealed class ITunesSearchResponse
{
    [JsonPropertyName("results")]
    public List<ITunesResult>? Results { get; set; }
}

internal sealed class ITunesResult
{
    [JsonPropertyName("trackName")]
    public string? TrackName { get; set; }

    [JsonPropertyName("artistName")]
    public string? ArtistName { get; set; }

    [JsonPropertyName("trackViewUrl")]
    public string? TrackViewUrl { get; set; }
}
