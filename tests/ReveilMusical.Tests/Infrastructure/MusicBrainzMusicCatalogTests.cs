using System.Text.Json;
using ReveilMusical.Application;
using ReveilMusical.Infrastructure.Music;
using ReveilMusical.Tests.Fakes;

namespace ReveilMusical.Tests.Infrastructure;

public class MusicBrainzMusicCatalogTests : MusicCatalogContractTests
{
    protected override string ExpectedSource => "musicbrainz";

    protected override string ContentType => "application/json";

    protected override string NotFoundBody => """{"created":"2026-01-05T07:00:00Z","count":0,"offset":0,"recordings":[]}""";

    protected override IMusicCatalog CreateCatalog(HttpClient http) => new MusicBrainzMusicCatalog(http);

    protected override string FoundBody(string title, string artist) => JsonSerializer.Serialize(new Dictionary<string, object>
    {
        ["count"] = 1,
        ["recordings"] = new[]
        {
            new Dictionary<string, object>
            {
                ["id"] = "b1a9c0e9-d987-4042-ae91-78d6a3267d69",
                ["title"] = title,
                ["artist-credit"] = new[] { new Dictionary<string, object> { ["name"] = artist } },
            },
        },
    });

    private static HttpClient ConfiguredClient(StubHttpMessageHandler handler, string userAgent)
    {
        var http = new HttpClient(handler);
        MusicBrainzMusicCatalog.ConfigureClient(http, userAgent);
        return http;
    }

    [Fact]
    public async Task FindAsync_Requete_PorteUnUserAgentIdentifiableAvecContact()
    {
        var handler = StubHttpMessageHandler.Json(NotFoundBody);
        var http = ConfiguredClient(handler, "ReveilMusical/1.0 (contact@exemple.fr)");

        await CreateCatalog(http).FindAsync("Purple Rain", CancellationToken.None);

        var userAgent = Assert.Single(handler.UserAgents);
        Assert.Contains("ReveilMusical/1.0", userAgent);
        Assert.Contains("contact@exemple.fr", userAgent);
    }

    [Fact]
    public async Task FindAsync_Requete_CibleLaRechercheDEnregistrementsEnJson()
    {
        var handler = StubHttpMessageHandler.Json(NotFoundBody);
        var http = ConfiguredClient(handler, "ReveilMusical/1.0 (contact@exemple.fr)");

        await CreateCatalog(http).FindAsync("Purple Rain", CancellationToken.None);

        Assert.Equal(
            "https://musicbrainz.org/ws/2/recording?query=Purple%20Rain&fmt=json&limit=5",
            Assert.Single(handler.RequestUris).AbsoluteUri);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ConfigureClient_UserAgentAbsent_EchoueExplicitement(string userAgent)
    {
        using var http = new HttpClient();

        var ex = Assert.Throws<InvalidOperationException>(() => MusicBrainzMusicCatalog.ConfigureClient(http, userAgent));
        Assert.Contains("UserAgent", ex.Message);
    }

    [Fact]
    public async Task FindAsync_PlusieursArtistes_SontConcatenes()
    {
        var handler = StubHttpMessageHandler.Json(
            """{"recordings":[{"title":"Under Pressure","artist-credit":[{"name":"Queen"},{"name":"David Bowie"}]}]}""");

        var track = await CreateCatalog(handler.CreateClient()).FindAsync("Under Pressure", CancellationToken.None);

        Assert.Equal("Queen, David Bowie", track!.Artist);
    }

    [Fact]
    public async Task FindAsync_SansCreditArtiste_RenvoieUnArtisteVide()
    {
        var handler = StubHttpMessageHandler.Json("""{"recordings":[{"title":"Purple Rain"}]}""");

        var track = await CreateCatalog(handler.CreateClient()).FindAsync("Purple Rain", CancellationToken.None);

        Assert.Equal("Purple Rain", track!.Title);
        Assert.Equal(string.Empty, track.Artist);
    }
}
