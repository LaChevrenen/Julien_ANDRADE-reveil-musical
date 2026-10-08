using Microsoft.Extensions.Logging;
using ReveilMusical.Application;
using ReveilMusical.Tests.Fakes;

namespace ReveilMusical.Tests.Application;

public class ResilientTrackResolverTests
{
    private readonly ListLogger<ResilientTrackResolver> _logger = new();
    private readonly FakeMusicCatalog _local = FakeMusicCatalog.Returning("local");

    private ResilientTrackResolver Build(params FakeMusicCatalog[] providers) => new(providers, _local, _logger);

    [Fact]
    public async Task ResolveAsync_PremierFournisseurRepond_NAppellePasLesSuivants()
    {
        var premier = FakeMusicCatalog.Returning("a");
        var second = FakeMusicCatalog.Returning("b");

        var resolution = await Build(premier, second).ResolveAsync("Purple Rain", CancellationToken.None);

        Assert.Equal("a", resolution.Track.Source);
        Assert.False(resolution.UsedLocalFallback);
        Assert.Equal(0, second.Calls);
        Assert.Equal(0, _local.Calls);
    }

    [Fact]
    public async Task ResolveAsync_PremierFournisseurLeve_BasculeSurLeSuivant()
    {
        var premier = FakeMusicCatalog.Throwing("a", new HttpRequestException("500"));
        var second = FakeMusicCatalog.Returning("b");

        var resolution = await Build(premier, second).ResolveAsync("Purple Rain", CancellationToken.None);

        Assert.Equal("b", resolution.Track.Source);
        Assert.False(resolution.UsedLocalFallback);
        Assert.Equal(1, _logger.Count(LogLevel.Warning));
    }

    [Fact]
    public async Task ResolveAsync_PremierFournisseurRenvoieNull_BasculeSurLeSuivant()
    {
        var resolution = await Build(FakeMusicCatalog.ReturningNull("a"), FakeMusicCatalog.Returning("b"))
            .ResolveAsync("Purple Rain", CancellationToken.None);

        Assert.Equal("b", resolution.Track.Source);
    }

    [Fact]
    public async Task ResolveAsync_Timeout_EstUnePanneEtBasculeSurLeSuivant()
    {
        // HttpClient signale un timeout par TaskCanceledException sans que l'appelant ait annulé.
        var premier = FakeMusicCatalog.Throwing("a", new TaskCanceledException("timeout"));

        var resolution = await Build(premier, FakeMusicCatalog.Returning("b"))
            .ResolveAsync("Purple Rain", CancellationToken.None);

        Assert.Equal("b", resolution.Track.Source);
    }

    [Fact]
    public async Task ResolveAsync_TousLesFournisseursEnPanne_UtiliseLeLocalEtLeSignale()
    {
        var resolution = await Build(
                FakeMusicCatalog.Throwing("a", new HttpRequestException("429")),
                FakeMusicCatalog.ReturningNull("b"))
            .ResolveAsync("Purple Rain", CancellationToken.None);

        Assert.Equal("local", resolution.Track.Source);
        Assert.True(resolution.UsedLocalFallback);
    }

    [Fact]
    public async Task ResolveAsync_AucunFournisseurConfigure_UtiliseLeLocal()
    {
        var resolution = await Build().ResolveAsync("Purple Rain", CancellationToken.None);

        Assert.True(resolution.UsedLocalFallback);
        Assert.Equal(1, _local.Calls);
    }

    [Fact]
    public async Task ResolveAsync_OrdreDesFournisseurs_EstCeluiFourni()
    {
        var journal = new List<string>();

        await Build(
                FakeMusicCatalog.ReturningNull("b", journal),
                FakeMusicCatalog.ReturningNull("a", journal))
            .ResolveAsync("Purple Rain", CancellationToken.None);

        Assert.Equal(["b", "a"], journal);
    }

    [Fact]
    public async Task ResolveAsync_LocalRompantSonContrat_RenvoieQuandMemeUnMorceau()
    {
        var resolver = new ResilientTrackResolver(
            [],
            FakeMusicCatalog.Throwing("local", new InvalidOperationException("impossible")),
            _logger);

        var resolution = await resolver.ResolveAsync("Purple Rain", CancellationToken.None);

        Assert.Equal("Purple Rain", resolution.Track.Title);
        Assert.True(resolution.UsedLocalFallback);
    }

    [Fact]
    public async Task ResolveAsync_AnnulationDemandee_EstPropagee()
    {
        using var cts = new CancellationTokenSource();
        var premier = FakeMusicCatalog.With("a", _ =>
        {
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        });
        var second = FakeMusicCatalog.Returning("b");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Build(premier, second).ResolveAsync("Purple Rain", cts.Token));
        Assert.Equal(0, second.Calls);
        Assert.Equal(0, _local.Calls);
    }

    [Fact]
    public async Task ResolveAsync_JetonDejaAnnule_NAppelleAucunFournisseur()
    {
        var premier = FakeMusicCatalog.Returning("a");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Build(premier).ResolveAsync("Purple Rain", new CancellationToken(canceled: true)));
        Assert.Equal(0, premier.Calls);
    }
}
