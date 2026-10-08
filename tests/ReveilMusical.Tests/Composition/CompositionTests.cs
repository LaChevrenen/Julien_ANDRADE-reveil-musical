using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReveilMusical.Application;
using ReveilMusical.Domain;
using ReveilMusical.Host;

namespace ReveilMusical.Tests.Composition;

public class CompositionTests
{
    private static readonly WakeUpRequest Request = new("alice", DayOfWeek.Monday, WeatherType.Pluie);

    [Fact]
    public void Conteneur_ConfigurationParDefaut_ResoutLePointDEntree()
    {
        using var provider = TestApp.Build(TestApp.Http(TestApp.ITunesNotFound, TestApp.MusicBrainzNotFound));

        Assert.IsType<WakeUpService>(provider.GetRequiredService<IWakeUpService>());
        Assert.IsType<ResilientTrackResolver>(provider.GetRequiredService<ITrackResolver>());
        Assert.IsType<DefaultingUserPreferencesProvider>(provider.GetRequiredService<IUserPreferencesProvider>());
        Assert.IsType<FallbackNotificationDispatcher>(provider.GetRequiredService<INotificationDispatcher>());
    }

    [Fact]
    public void Conteneur_TroisCanauxEnregistres_SansModifierLeDispatcher()
    {
        using var provider = TestApp.Build(TestApp.Http(TestApp.ITunesNotFound, TestApp.MusicBrainzNotFound));

        var ids = provider.GetServices<INotificationChannel>().Select(c => c.Id.Value).Order().ToList();

        Assert.Equal(["email", "push", "sms"], ids);
    }

    [Fact]
    public void Conteneur_DureesDeVie_CacheSingletonEtServicesTransient()
    {
        using var provider = TestApp.Build(TestApp.Http(TestApp.ITunesNotFound, TestApp.MusicBrainzNotFound));

        Assert.Same(provider.GetRequiredService<ICache>(), provider.GetRequiredService<ICache>());
        Assert.NotSame(provider.GetRequiredService<IWakeUpService>(), provider.GetRequiredService<IWakeUpService>());
        Assert.NotSame(provider.GetRequiredService<ITrackResolver>(), provider.GetRequiredService<ITrackResolver>());
    }

    [Theory]
    [InlineData("itunes", "musicbrainz")]
    [InlineData("musicbrainz", "itunes")]
    public async Task MusicProviders_OrdreDeLaConfiguration_EstLOrdreDesAppelsSansRecompiler(string premier, string second)
    {
        var http = TestApp.Http(TestApp.ITunesNotFound, TestApp.MusicBrainzNotFound);
        using var provider = TestApp.Build(http, settings =>
        {
            settings["Music:Providers:0"] = premier;
            settings["Music:Providers:1"] = second;
        });

        var result = await provider.GetRequiredService<IWakeUpService>().SendWakeUpAsync(Request);

        var appels = http.RequestUris.Select(u => u.Host == TestApp.ITunesHost ? "itunes" : "musicbrainz").ToList();
        Assert.Equal([premier, second], appels);
        Assert.Equal("local", result.Track.Source);
    }

    [Fact]
    public async Task MusicProviders_UnSeulFournisseur_NAppelleQueLui()
    {
        var http = TestApp.Http(TestApp.ITunesNotFound, TestApp.MusicBrainzFound("Purple Rain", "Prince"));
        using var provider = TestApp.Build(http, settings =>
        {
            settings["Music:Providers:0"] = " MusicBrainz ";
            settings.Remove("Music:Providers:1");
        });

        var result = await provider.GetRequiredService<IWakeUpService>().SendWakeUpAsync(Request);

        Assert.Equal(TestApp.MusicBrainzHost, Assert.Single(http.RequestUris).Host);
        Assert.Equal("musicbrainz", result.Track.Source);
        Assert.False(result.Degraded);
    }

    [Fact]
    public async Task MusicProviders_ListeVide_UtiliseLeCatalogueLocalSansAppelReseau()
    {
        var http = TestApp.Http(null, null);
        using var provider = TestApp.Build(http, settings =>
        {
            settings.Remove("Music:Providers:0");
            settings.Remove("Music:Providers:1");
        });

        var result = await provider.GetRequiredService<IWakeUpService>().SendWakeUpAsync(Request);

        Assert.Equal(0, http.Calls);
        Assert.Equal("local", result.Track.Source);
    }

    [Fact]
    public void MusicProviders_NomInconnu_EchoueExplicitementEnListantLesNomsValides()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => TestApp.Build(
            TestApp.Http(null, null),
            settings => settings["Music:Providers:1"] = "spotify"));

        Assert.Contains("spotify", ex.Message);
        Assert.Contains("itunes, local, musicbrainz", ex.Message);
    }

    [Theory]
    [InlineData("Notification:DefaultChannel")]
    [InlineData("Defaults:FallbackTrack")]
    public void Configuration_CleObligatoireAbsente_EchoueExplicitement(string cle)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => TestApp.Build(
            TestApp.Http(null, null),
            settings => settings.Remove(cle)));

        Assert.Contains(cle, ex.Message);
    }

    [Theory]
    [InlineData("Music:HttpTimeoutSeconds")]
    [InlineData("Music:CacheTtlMinutes")]
    public void Configuration_DureeNulle_EchoueExplicitement(string cle)
    {
        Assert.Throws<InvalidOperationException>(() => TestApp.Build(
            TestApp.Http(null, null),
            settings => settings[cle] = "0"));
    }

    [Fact]
    public async Task MusicBrainz_UserAgentDeLaConfiguration_EstPorteParLaRequete()
    {
        var http = TestApp.Http(TestApp.ITunesNotFound, TestApp.MusicBrainzNotFound);
        using var provider = TestApp.Build(http, settings =>
        {
            settings["Music:Providers:0"] = "musicbrainz";
            settings.Remove("Music:Providers:1");
        });

        await provider.GetRequiredService<IWakeUpService>().SendWakeUpAsync(Request);

        var userAgent = Assert.Single(http.UserAgents);
        Assert.Contains("ReveilMusical/1.0", userAgent);
        Assert.Contains("tests@exemple.fr", userAgent);
    }

    [Fact]
    public async Task Cache_DeuxReveilsIdentiques_UnSeulAppelAuFournisseur()
    {
        var http = TestApp.Http(TestApp.ITunesFound("Purple Rain", "Prince"), TestApp.MusicBrainzNotFound);
        using var provider = TestApp.Build(http);

        await provider.GetRequiredService<IWakeUpService>().SendWakeUpAsync(Request);
        await provider.GetRequiredService<IWakeUpService>().SendWakeUpAsync(Request);

        Assert.Equal(1, http.Calls);
    }

    [Fact]
    public void AppSettings_FichierLivre_ConstruitUnConteneurValide()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();
        var services = new ServiceCollection();

        services.AddReveilMusical(configuration);

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        Assert.NotNull(provider.GetRequiredService<IWakeUpService>());
        Assert.Equal(new ChannelId("push"), provider.GetRequiredService<NotificationPolicy>().DefaultChannel);
        Assert.Equal(["push", "sms", "email"], provider.GetRequiredService<NotificationPolicy>().FallbackOrder.Select(c => c.Value));
        Assert.Equal("Here Comes the Sun", provider.GetRequiredService<WakeUpDefaults>().FallbackTrack);
    }
}
