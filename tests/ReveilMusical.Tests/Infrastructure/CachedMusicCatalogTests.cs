using ReveilMusical.Domain;
using ReveilMusical.Infrastructure.Caching;
using ReveilMusical.Tests.Fakes;

namespace ReveilMusical.Tests.Infrastructure;

public class CachedMusicCatalogTests
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(60);

    private readonly FakeTimeProvider _clock = new();
    private readonly InMemoryCache _cache;

    public CachedMusicCatalogTests()
    {
        _cache = new InMemoryCache(_clock);
    }

    private CachedMusicCatalog Decorate(FakeMusicCatalog inner, string ns = "itunes") => new(inner, _cache, ns, Ttl);

    [Fact]
    public async Task FindAsync_DeuxAppelsIdentiques_UnSeulAppelInterne()
    {
        var inner = FakeMusicCatalog.Returning("itunes");
        var cached = Decorate(inner);

        var premier = await cached.FindAsync("Purple Rain", CancellationToken.None);
        var second = await cached.FindAsync("Purple Rain", CancellationToken.None);

        Assert.Equal(1, inner.Calls);
        Assert.Equal(premier, second);
    }

    [Fact]
    public async Task FindAsync_ClesNormalisees_CasseEtEspacesIgnores()
    {
        var inner = FakeMusicCatalog.Returning("itunes");
        var cached = Decorate(inner);

        await cached.FindAsync("Purple Rain", CancellationToken.None);
        await cached.FindAsync("  PURPLE rain ", CancellationToken.None);

        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public async Task FindAsync_ResultatNull_NEstPasMisEnCache()
    {
        var inner = FakeMusicCatalog.ReturningNull("itunes");
        var cached = Decorate(inner);

        Assert.Null(await cached.FindAsync("Introuvable", CancellationToken.None));
        Assert.Null(await cached.FindAsync("Introuvable", CancellationToken.None));

        Assert.Equal(2, inner.Calls);
    }

    [Fact]
    public async Task FindAsync_Echec_NEstPasMisEnCacheEtEstPropage()
    {
        var enPanne = true;
        var inner = FakeMusicCatalog.With("itunes", q =>
            enPanne ? throw new HttpRequestException("500") : new Track(q, "Prince", "itunes"));
        var cached = Decorate(inner);

        await Assert.ThrowsAsync<HttpRequestException>(() => cached.FindAsync("Purple Rain", CancellationToken.None));
        enPanne = false;
        var track = await cached.FindAsync("Purple Rain", CancellationToken.None);

        Assert.NotNull(track);
        Assert.Equal(2, inner.Calls);
    }

    [Fact]
    public async Task FindAsync_ApresExpirationDuTtl_RappelleLeFournisseur()
    {
        var inner = FakeMusicCatalog.Returning("itunes");
        var cached = Decorate(inner);

        await cached.FindAsync("Purple Rain", CancellationToken.None);
        _clock.Advance(Ttl + TimeSpan.FromSeconds(1));
        await cached.FindAsync("Purple Rain", CancellationToken.None);

        Assert.Equal(2, inner.Calls);
    }

    [Fact]
    public async Task FindAsync_DeuxFournisseursMemeCache_NeSePartagentPasLesEntrees()
    {
        var itunes = FakeMusicCatalog.Returning("itunes");
        var musicBrainz = FakeMusicCatalog.Returning("musicbrainz");

        await Decorate(itunes, "itunes").FindAsync("Purple Rain", CancellationToken.None);
        var track = await Decorate(musicBrainz, "musicbrainz").FindAsync("Purple Rain", CancellationToken.None);

        Assert.Equal(1, musicBrainz.Calls);
        Assert.Equal("musicbrainz", track!.Source);
    }
}
