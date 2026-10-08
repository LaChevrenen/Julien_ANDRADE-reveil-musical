using System.Text.Json;
using ReveilMusical.Application;
using ReveilMusical.Infrastructure.Music;
using ReveilMusical.Tests.Fakes;

namespace ReveilMusical.Tests.Infrastructure;

public class ITunesMusicCatalogTests : MusicCatalogContractTests
{
    private const string TrackViewUrl = "https://music.apple.com/fr/album/fuite-interdite/42";

    protected override string ExpectedSource => "itunes";

    // iTunes ne répond pas en application/json : l'adaptateur doit quand même lire le corps.
    protected override string ContentType => "text/javascript";

    protected override string NotFoundBody => """{"resultCount":0,"results":[]}""";

    protected override IMusicCatalog CreateCatalog(HttpClient http) => new ITunesMusicCatalog(http);

    protected override string FoundBody(string title, string artist) => JsonSerializer.Serialize(new
    {
        resultCount = 1,
        results = new[]
        {
            new { wrapperType = "track", trackName = title, artistName = artist, trackViewUrl = TrackViewUrl },
        },
    });

    [Fact]
    public async Task FindAsync_Requete_CibleLaRechercheMusicaleLimiteeACinq()
    {
        var handler = StubHttpMessageHandler.Json(NotFoundBody, ContentType);

        await CreateCatalog(handler.CreateClient("https://itunes.apple.com/")).FindAsync("Purple Rain", CancellationToken.None);

        Assert.Equal(
            "https://itunes.apple.com/search?term=Purple%20Rain&media=music&limit=5",
            Assert.Single(handler.RequestUris).AbsoluteUri);
    }

    [Fact]
    public async Task FindAsync_TrackViewUrl_NEstJamaisExpose()
    {
        var handler = StubHttpMessageHandler.Json(FoundBody("Purple Rain", "Prince"), ContentType);

        var track = await CreateCatalog(handler.CreateClient()).FindAsync("Purple Rain", CancellationToken.None);

        Assert.DoesNotContain("music.apple.com", track!.ToString());
        Assert.DoesNotContain(TrackViewUrl, new[] { track.Title, track.Artist, track.Source });
    }

    [Fact]
    public async Task FindAsync_PremierResultatSansTitre_PrendLeSuivant()
    {
        var handler = StubHttpMessageHandler.Json(
            """{"results":[{"artistName":"Podcast"},{"trackName":"Purple Rain","artistName":"Prince"}]}""",
            ContentType);

        var track = await CreateCatalog(handler.CreateClient()).FindAsync("Purple Rain", CancellationToken.None);

        Assert.Equal("Purple Rain", track!.Title);
    }

    [Fact]
    public async Task FindAsync_ArtisteAbsent_RenvoieUnArtisteVide()
    {
        var handler = StubHttpMessageHandler.Json("""{"results":[{"trackName":"Purple Rain"}]}""", ContentType);

        var track = await CreateCatalog(handler.CreateClient()).FindAsync("Purple Rain", CancellationToken.None);

        Assert.Equal(string.Empty, track!.Artist);
    }
}
