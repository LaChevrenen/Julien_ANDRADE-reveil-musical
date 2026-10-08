using System.Reflection;
using System.Runtime.CompilerServices;
using ReveilMusical.Application;
using ReveilMusical.Domain;
using ReveilMusical.Host;
using ReveilMusical.Infrastructure;
using ReveilMusical.Infrastructure.Music;

namespace ReveilMusical.Tests.Architecture;

/// <summary>Règles d'architecture vérifiées par réflexion, sans package supplémentaire.</summary>
public class ArchitectureTests
{
    private static readonly Assembly Domain = typeof(Track).Assembly;
    private static readonly Assembly Application = typeof(IWakeUpService).Assembly;
    private static readonly Assembly Infrastructure = typeof(ServiceCollectionExtensions).Assembly;
    private static readonly Assembly Host = typeof(CompositionRoot).Assembly;

    private static readonly string[] TechnicalTerms =
    [
        "ITunes", "MusicBrainz", "Smtp", "Sms", "Email", "Mail", "Push", "WhatsApp", "Http", "Json", "Url", "Dto",
    ];

    private static List<string> References(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();

    [Fact]
    public void Domain_NeReferenceAucunProjetNiPackage()
    {
        // « netstandard » n'apparaît que sous instrumentation de couverture ; c'est le framework, pas un package.
        Assert.All(References(Domain), name => Assert.True(
            name == "netstandard" || name.StartsWith("System.", StringComparison.Ordinal),
            $"Référence inattendue : {name}"));
    }

    [Fact]
    public void Application_NeReferenceNiInfrastructureNiHost()
    {
        var references = References(Application);

        Assert.Contains("ReveilMusical.Domain", references);
        Assert.DoesNotContain("ReveilMusical.Infrastructure", references);
        Assert.DoesNotContain("ReveilMusical.Host", references);
    }

    [Fact]
    public void Application_NeConnaitPasHttp()
    {
        Assert.DoesNotContain(References(Application), name => name.StartsWith("System.Net", StringComparison.Ordinal));
        Assert.DoesNotContain("System.Text.Json", References(Application));
    }

    [Fact]
    public void Infrastructure_NeReferencePasLeHost()
    {
        Assert.DoesNotContain("ReveilMusical.Host", References(Infrastructure));
    }

    [Fact]
    public void DomainEtApplication_AucunTypePublicNePorteUnNomDeFournisseurOuDeCanal()
    {
        var fautifs = Domain.GetExportedTypes().Concat(Application.GetExportedTypes())
            .Where(type => TechnicalTerms.Any(term => type.Name.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .Select(type => type.FullName);

        Assert.Empty(fautifs);
    }

    [Fact]
    public void DomainEtApplication_AucunMembrePublicNePorteUnDetailDeFournisseur()
    {
        var fautifs = Domain.GetExportedTypes().Concat(Application.GetExportedTypes())
            .SelectMany(type => type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(member => TechnicalTerms.Any(term => member.Name.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .Select(member => $"{member.DeclaringType!.Name}.{member.Name}");

        Assert.Empty(fautifs);
    }

    [Fact]
    public void Track_NExposeQueTitreArtisteEtSource()
    {
        var proprietes = typeof(Track).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(p => p.Name)
            .Order();

        Assert.Equal(["Artist", "Source", "Title"], proprietes);
    }

    [Theory]
    [InlineData(typeof(ITunesSearchResponse))]
    [InlineData(typeof(ITunesResult))]
    [InlineData(typeof(MusicBrainzSearchResponse))]
    [InlineData(typeof(MusicBrainzRecording))]
    [InlineData(typeof(MusicBrainzArtistCredit))]
    public void DtoFournisseur_EstInternal(Type dto)
    {
        Assert.True(dto.IsNotPublic);
    }

    [Fact]
    public void Infrastructure_NExposePubliquementQueServiceCollectionExtensions()
    {
        Assert.Equal([typeof(ServiceCollectionExtensions)], Infrastructure.GetExportedTypes());
    }

    [Fact]
    public void Sources_AucunChampStatiqueNonConstant()
    {
        var fautifs = new[] { Domain, Application, Infrastructure, Host }
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => !type.IsDefined(typeof(CompilerGeneratedAttribute)) && !type.Name.Contains('<'))
            .SelectMany(type => type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            .Where(field => !field.IsLiteral && !field.Name.Contains('<'))
            .Select(field => $"{field.DeclaringType!.Name}.{field.Name}");

        Assert.Empty(fautifs);
    }
}
