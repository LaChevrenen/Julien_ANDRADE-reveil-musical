using ReveilMusical.Domain;
using ReveilMusical.Infrastructure.Caching;
using ReveilMusical.Tests.Fakes;

namespace ReveilMusical.Tests.Infrastructure;

public class InMemoryCacheTests
{
    private static readonly Track Morceau = new("Purple Rain", "Prince", "itunes");

    private readonly FakeTimeProvider _clock = new();
    private readonly InMemoryCache _cache;

    public InMemoryCacheTests()
    {
        _cache = new InMemoryCache(_clock);
    }

    [Fact]
    public async Task GetAsync_CleAbsente_RenvoieNull()
    {
        Assert.Null(await _cache.GetAsync<Track>("absente", CancellationToken.None));
    }

    [Fact]
    public async Task GetAsync_ApresSet_RenvoieLaValeur()
    {
        await _cache.SetAsync("k", Morceau, TimeSpan.FromMinutes(5), CancellationToken.None);

        Assert.Equal(Morceau, await _cache.GetAsync<Track>("k", CancellationToken.None));
    }

    [Fact]
    public async Task GetAsync_ApresExpirationDuTtl_RenvoieNull()
    {
        await _cache.SetAsync("k", Morceau, TimeSpan.FromMinutes(5), CancellationToken.None);

        _clock.Advance(TimeSpan.FromMinutes(5));

        Assert.Null(await _cache.GetAsync<Track>("k", CancellationToken.None));
    }

    [Fact]
    public async Task GetAsync_JusteAvantExpiration_RenvoieEncoreLaValeur()
    {
        await _cache.SetAsync("k", Morceau, TimeSpan.FromMinutes(5), CancellationToken.None);

        _clock.Advance(TimeSpan.FromMinutes(4));

        Assert.NotNull(await _cache.GetAsync<Track>("k", CancellationToken.None));
    }

    [Fact]
    public async Task GetAsync_TypeDifferentDeCeluiStocke_RenvoieNull()
    {
        await _cache.SetAsync("k", "une chaîne", TimeSpan.FromMinutes(5), CancellationToken.None);

        Assert.Null(await _cache.GetAsync<Track>("k", CancellationToken.None));
    }

    [Fact]
    public async Task DeuxInstances_NePartagentAucunEtat()
    {
        var autre = new InMemoryCache(_clock);
        await _cache.SetAsync("k", Morceau, TimeSpan.FromMinutes(5), CancellationToken.None);

        Assert.Null(await autre.GetAsync<Track>("k", CancellationToken.None));
    }

    [Fact]
    public async Task GetEtSet_JetonAnnule_PropagentLAnnulation()
    {
        var annule = new CancellationToken(canceled: true);

        await Assert.ThrowsAsync<OperationCanceledException>(() => _cache.GetAsync<Track>("k", annule));
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _cache.SetAsync("k", Morceau, TimeSpan.FromMinutes(5), annule));
    }
}
