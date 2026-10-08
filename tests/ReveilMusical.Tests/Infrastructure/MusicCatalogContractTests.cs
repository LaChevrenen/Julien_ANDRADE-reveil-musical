using System.Net;
using ReveilMusical.Application;
using ReveilMusical.Tests.Fakes;

namespace ReveilMusical.Tests.Infrastructure;

/// <summary>
/// Contrat commun à tout fournisseur musical réseau. Une sous-classe par adaptateur
/// fournit seulement la fabrique et les corps JSON propres au fournisseur.
/// </summary>
public abstract class MusicCatalogContractTests
{
    protected abstract string ExpectedSource { get; }

    protected abstract string ContentType { get; }

    protected abstract IMusicCatalog CreateCatalog(HttpClient http);

    protected abstract string FoundBody(string title, string artist);

    /// <summary>Réponse valide du fournisseur ne contenant aucun résultat.</summary>
    protected abstract string NotFoundBody { get; }

    private IMusicCatalog CatalogReturning(string body, out StubHttpMessageHandler handler)
    {
        handler = StubHttpMessageHandler.Json(body, ContentType);
        return CreateCatalog(handler.CreateClient());
    }

    [Fact]
    public async Task FindAsync_MorceauTrouve_RenvoieTitreArtisteEtSource()
    {
        var catalog = CatalogReturning(FoundBody("Purple Rain", "Prince"), out _);

        var track = await catalog.FindAsync("Purple Rain", CancellationToken.None);

        Assert.NotNull(track);
        Assert.Equal("Purple Rain", track.Title);
        Assert.Equal("Prince", track.Artist);
        Assert.Equal(ExpectedSource, track.Source);
    }

    [Fact]
    public async Task FindAsync_Introuvable_RenvoieNull()
    {
        var catalog = CatalogReturning(NotFoundBody, out _);

        Assert.Null(await catalog.FindAsync("zzzz introuvable", CancellationToken.None));
    }

    [Fact]
    public async Task FindAsync_ReponseVide_RenvoieNull()
    {
        var catalog = CatalogReturning("{}", out _);

        Assert.Null(await catalog.FindAsync("Purple Rain", CancellationToken.None));
    }

    [Fact]
    public async Task FindAsync_Accents_RequeteEncodeeEtReponsePreservee()
    {
        var catalog = CatalogReturning(FoundBody("L'Été indien", "Édith Piaf"), out var handler);

        var track = await catalog.FindAsync("Été & pluie", CancellationToken.None);

        var uri = Assert.Single(handler.RequestUris).AbsoluteUri;
        Assert.Contains("%C3%89t%C3%A9%20%26%20pluie", uri);
        Assert.Equal("L'Été indien", track!.Title);
        Assert.Equal("Édith Piaf", track.Artist);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task FindAsync_HttpEnErreur_LeveSansAvaler(HttpStatusCode status)
    {
        var catalog = CreateCatalog(StubHttpMessageHandler.Status(status).CreateClient());

        await Assert.ThrowsAsync<HttpRequestException>(() => catalog.FindAsync("Purple Rain", CancellationToken.None));
    }

    [Fact]
    public async Task FindAsync_Timeout_Leve()
    {
        var catalog = CreateCatalog(
            StubHttpMessageHandler.Throwing(new TaskCanceledException("timeout")).CreateClient());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => catalog.FindAsync("Purple Rain", CancellationToken.None));
    }

    [Fact]
    public async Task FindAsync_JsonInvalide_Leve()
    {
        var catalog = CatalogReturning("<html>maintenance</html>", out _);

        await Assert.ThrowsAnyAsync<Exception>(() => catalog.FindAsync("Purple Rain", CancellationToken.None));
    }

    [Fact]
    public async Task FindAsync_AnnulationDemandee_EstPropagee()
    {
        var catalog = CatalogReturning(FoundBody("Purple Rain", "Prince"), out var handler);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => catalog.FindAsync("Purple Rain", new CancellationToken(canceled: true)));
        Assert.Equal(0, handler.Calls);
    }
}
