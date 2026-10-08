using ReveilMusical.Infrastructure.Music;

namespace ReveilMusical.Tests.Infrastructure;

public class LocalMusicCatalogTests
{
    private readonly LocalMusicCatalog _catalog = new();

    [Theory]
    [InlineData("Purple Rain", "Purple Rain")]
    [InlineData("purple", "Purple Rain")]
    [InlineData("  WALKING ON SUNSHINE ", "Walking on Sunshine")]
    [InlineData("l'ete indien", "L'Été indien")]
    [InlineData("L'ÉTÉ INDIEN", "L'Été indien")]
    [InlineData("Let It Snow (Remastered)", "Let It Snow")]
    public async Task FindAsync_SousChaineSansCasseNiAccents_TrouveLeMorceau(string requete, string attendu)
    {
        var track = await _catalog.FindAsync(requete, CancellationToken.None);

        Assert.Equal(attendu, track!.Title);
    }

    [Theory]
    [InlineData("Morceau totalement inconnu")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task FindAsync_RequeteSansCorrespondance_RenvoieLeMorceauParDefautSansLever(string? requete)
    {
        var track = await _catalog.FindAsync(requete!, CancellationToken.None);

        Assert.NotNull(track);
        Assert.Equal("Here Comes the Sun", track.Title);
    }

    [Fact]
    public async Task FindAsync_Toujours_EtiquetteLaSourceLocale()
    {
        var track = await _catalog.FindAsync("Purple Rain", CancellationToken.None);

        Assert.Equal("local", track!.Source);
        Assert.False(string.IsNullOrWhiteSpace(track.Artist));
    }

    [Fact]
    public async Task FindAsync_MemeRequete_EstDeterministe()
    {
        var premier = await _catalog.FindAsync("inconnu", CancellationToken.None);
        var second = await _catalog.FindAsync("inconnu", CancellationToken.None);

        Assert.Equal(premier, second);
    }

    [Fact]
    public async Task FindAsync_JetonAnnule_NeLevePas()
    {
        var track = await _catalog.FindAsync("Purple Rain", new CancellationToken(canceled: true));

        Assert.NotNull(track);
    }
}
