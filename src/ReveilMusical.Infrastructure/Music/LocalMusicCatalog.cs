using System.Globalization;
using System.Text;
using ReveilMusical.Application;
using ReveilMusical.Domain;

namespace ReveilMusical.Infrastructure.Music;

/// <summary>
/// Dernier recours : liste en dur. Ne renvoie jamais <c>null</c> et ne lève que sur annulation.
/// </summary>
internal sealed class LocalMusicCatalog : IMusicCatalog
{
    public const string SourceName = "local";

    private static readonly IReadOnlyList<Track> Tracks =
    [
        new("Here Comes the Sun", "The Beatles", SourceName),
        new("Walking on Sunshine", "Katrina and the Waves", SourceName),
        new("Purple Rain", "Prince", SourceName),
        new("Let It Snow", "Dean Martin", SourceName),
        new("Both Sides, Now", "Joni Mitchell", SourceName),
        new("L'Été indien", "Joe Dassin", SourceName),
        new("Non, je ne regrette rien", "Édith Piaf", SourceName),
    ];

    public Task<Track?> FindAsync(string query, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<Track?>(Find(query));
    }

    private static Track Find(string? query)
    {
        var wanted = Normalize(query);
        if (wanted.Length == 0)
        {
            return Tracks[0];
        }

        foreach (var track in Tracks)
        {
            var title = Normalize(track.Title);
            if (title.Contains(wanted, StringComparison.Ordinal) || wanted.Contains(title, StringComparison.Ordinal))
            {
                return track;
            }
        }

        return Tracks[0];
    }

    /// <summary>Minuscules sans accents, indépendamment de la culture courante.</summary>
    private static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        foreach (var c in text.Trim().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        return builder.ToString();
    }
}
