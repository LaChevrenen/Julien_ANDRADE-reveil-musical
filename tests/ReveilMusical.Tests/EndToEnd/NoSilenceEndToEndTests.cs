using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ReveilMusical.Application;
using ReveilMusical.Domain;
using ReveilMusical.Tests.Composition;
using ReveilMusical.Tests.Fakes;

namespace ReveilMusical.Tests.EndToEnd;

/// <summary>
/// « Pas de silence » : vrai conteneur DI, HTTP simulé, canaux simulés.
/// alice préfère l'email et veut « Purple Rain » quand il pleut.
/// </summary>
public class NoSilenceEndToEndTests
{
    private static readonly WakeUpRequest Request = new("alice", DayOfWeek.Monday, WeatherType.Pluie);

    private readonly List<string> _journal = [];
    private readonly FakeNotificationChannel _email;
    private readonly FakeNotificationChannel _sms;
    private readonly FakeNotificationChannel _push;

    public NoSilenceEndToEndTests()
    {
        _email = new FakeNotificationChannel("email", journal: _journal);
        _sms = new FakeNotificationChannel("sms", journal: _journal);
        _push = new FakeNotificationChannel("push", journal: _journal);
    }

    private ServiceProvider Build(StubHttpMessageHandler http, Action<IServiceCollection>? customize = null) =>
        TestApp.Build(http, customize: services =>
        {
            services.RemoveAll<INotificationChannel>();
            services.AddSingleton<INotificationChannel>(_email);
            services.AddSingleton<INotificationChannel>(_sms);
            services.AddSingleton<INotificationChannel>(_push);
            customize?.Invoke(services);
        });

    private static Task<WakeUpResult> Send(ServiceProvider provider, WakeUpRequest? request = null, CancellationToken ct = default) =>
        provider.GetRequiredService<IWakeUpService>().SendWakeUpAsync(request ?? Request, ct);

    private static void ReplacePreferences(IServiceCollection services, IUserPreferencesProvider source) =>
        services.AddKeyedSingleton(ServiceKeys.PreferencesSource, source);

    [Fact]
    public async Task ToutDisponible_MorceauDuPremierFournisseurSurLeCanalPrefere()
    {
        var http = TestApp.Http(TestApp.ITunesFound("Purple Rain", "Prince"), TestApp.MusicBrainzNotFound);
        using var provider = Build(http);

        var result = await Send(provider);

        Assert.Equal(new Track("Purple Rain", "Prince", "itunes"), result.Track);
        Assert.Equal(new ChannelId("email"), result.ChannelUsed);
        Assert.False(result.Degraded);
        var (userId, message) = Assert.Single(_email.Sent);
        Assert.Equal("alice", userId);
        Assert.Equal("Bonjour ! Nous sommes lundi et il pleut. Votre morceau du réveil : « Purple Rain » de Prince.", message.Text);
        Assert.Equal(TestApp.ITunesHost, Assert.Single(http.RequestUris).Host);
    }

    [Fact]
    public async Task ITunesEnPanne_BasculeSurMusicBrainzSansDegradation()
    {
        var http = TestApp.Http(null, TestApp.MusicBrainzFound("Purple Rain", "Prince"));
        using var provider = Build(http);

        var result = await Send(provider);

        Assert.Equal("musicbrainz", result.Track.Source);
        Assert.Equal(new ChannelId("email"), result.ChannelUsed);
        Assert.False(result.Degraded);
        Assert.Equal([TestApp.ITunesHost, TestApp.MusicBrainzHost], http.RequestUris.Select(u => u.Host));
    }

    [Fact]
    public async Task ITunesEnTimeout_BasculeSurMusicBrainz()
    {
        var http = new StubHttpMessageHandler(request => request.RequestUri!.Host == TestApp.ITunesHost
            ? throw new TaskCanceledException("timeout")
            : StubHttpMessageHandler.JsonResponse(TestApp.MusicBrainzFound("Purple Rain", "Prince")));
        using var provider = Build(http);

        var result = await Send(provider);

        Assert.Equal("musicbrainz", result.Track.Source);
    }

    [Fact]
    public async Task DeuxFournisseursEnPanne_MorceauLocalEtModeDegrade()
    {
        using var provider = Build(TestApp.Http(null, null));

        var result = await Send(provider);

        Assert.Equal(new Track("Purple Rain", "Prince", "local"), result.Track);
        Assert.True(result.Degraded);
        Assert.Equal(new ChannelId("email"), result.ChannelUsed);
        Assert.Single(_email.Sent);
    }

    [Fact]
    public async Task PanneDeFournisseur_LeRepliLocalNEstPasMisEnCache()
    {
        var enPanne = true;
        var http = new StubHttpMessageHandler(_ => enPanne
            ? new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable)
            : StubHttpMessageHandler.JsonResponse(TestApp.ITunesFound("Purple Rain", "Prince")));
        using var provider = Build(http);

        var pendant = await Send(provider);
        enPanne = false;
        var apres = await Send(provider);

        Assert.Equal("local", pendant.Track.Source);
        Assert.Equal("itunes", apres.Track.Source);
        Assert.False(apres.Degraded);
    }

    [Fact]
    public async Task CanalPrefereEnPanne_RepliSurLOrdreConfigure()
    {
        _email.Fails = true;
        using var provider = Build(TestApp.Http(TestApp.ITunesFound("Purple Rain", "Prince"), TestApp.MusicBrainzNotFound));

        var result = await Send(provider);

        Assert.Equal(new ChannelId("push"), result.ChannelUsed);
        Assert.True(result.Degraded);
        Assert.Equal(["email", "push"], _journal);
        Assert.Single(_push.Sent);
    }

    [Fact]
    public async Task CanalPrefereInconnu_RepliSurUnCanalEnregistre()
    {
        var prefs = new UserPreferences(new Dictionary<WeatherType, string>(), "Purple Rain", new ChannelId("whatsapp"));
        using var provider = Build(
            TestApp.Http(TestApp.ITunesFound("Purple Rain", "Prince"), TestApp.MusicBrainzNotFound),
            services => ReplacePreferences(services, new FakeUserPreferencesProvider(prefs)));

        var result = await Send(provider);

        Assert.Equal(new ChannelId("push"), result.ChannelUsed);
        Assert.True(result.Degraded);
    }

    [Fact]
    public async Task NouveauCanalEnregistre_EstUtiliseSansModifierLExistant()
    {
        var whatsapp = new FakeNotificationChannel("whatsapp");
        var prefs = new UserPreferences(new Dictionary<WeatherType, string>(), "Purple Rain", new ChannelId("WhatsApp"));
        using var provider = Build(
            TestApp.Http(TestApp.ITunesFound("Purple Rain", "Prince"), TestApp.MusicBrainzNotFound),
            services =>
            {
                services.AddSingleton<INotificationChannel>(whatsapp);
                ReplacePreferences(services, new FakeUserPreferencesProvider(prefs));
            });

        var result = await Send(provider);

        Assert.Equal(new ChannelId("whatsapp"), result.ChannelUsed);
        Assert.False(result.Degraded);
        Assert.Single(whatsapp.Sent);
    }

    [Fact]
    public async Task PreferencesEnPanne_ReveilEnvoyeAvecLesDefautsDeLaConfiguration()
    {
        using var provider = Build(
            TestApp.Http(TestApp.ITunesFound("Here Comes the Sun", "The Beatles"), TestApp.MusicBrainzNotFound),
            services => ReplacePreferences(services, FakeUserPreferencesProvider.Throwing(new TimeoutException("préférences muettes"))));

        var result = await Send(provider);

        Assert.Equal("Here Comes the Sun", result.Track.Title);
        Assert.Equal(new ChannelId("push"), result.ChannelUsed);
        Assert.Single(_push.Sent);
    }

    [Fact]
    public async Task UtilisateurInconnu_ReveilEnvoyeAvecLesDefauts()
    {
        using var provider = Build(TestApp.Http(TestApp.ITunesFound("Here Comes the Sun", "The Beatles"), TestApp.MusicBrainzNotFound));

        var result = await Send(provider, new WakeUpRequest("inconnu-42", DayOfWeek.Sunday, WeatherType.Neige));

        Assert.Equal("Here Comes the Sun", result.Track.Title);
        Assert.Equal(new ChannelId("push"), result.ChannelUsed);
    }

    [Fact]
    public async Task MeteoNonCouverte_MorceauDeSecoursDeLUtilisateur()
    {
        var http = TestApp.Http(TestApp.ITunesFound("Eye of the Tiger", "Survivor"), TestApp.MusicBrainzNotFound);
        using var provider = Build(http);

        var result = await Send(provider, new WakeUpRequest("bob", DayOfWeek.Saturday, WeatherType.Neige));

        Assert.Contains("term=Eye%20of%20the%20Tiger", Assert.Single(http.RequestUris).AbsoluteUri);
        Assert.Equal(new ChannelId("sms"), result.ChannelUsed);
        Assert.False(result.Degraded);
    }

    [Fact]
    public async Task ToutEnPanne_AucuneExceptionMorceauLocalEtCanalNull()
    {
        _email.Fails = true;
        _sms.Fails = true;
        _push.Fails = true;
        using var provider = Build(
            TestApp.Http(null, null),
            services => ReplacePreferences(services, FakeUserPreferencesProvider.Throwing(new InvalidOperationException("base hors service"))));

        var result = await Send(provider);

        Assert.Null(result.ChannelUsed);
        Assert.True(result.Degraded);
        Assert.Equal(new Track("Here Comes the Sun", "The Beatles", "local"), result.Track);
        Assert.Equal(["push", "sms", "email"], _journal);
    }

    [Fact]
    public async Task Annulation_EstPropageeEtRienNEstEnvoye()
    {
        using var provider = Build(TestApp.Http(TestApp.ITunesFound("Purple Rain", "Prince"), TestApp.MusicBrainzNotFound));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Send(provider, ct: new CancellationToken(canceled: true)));
        Assert.Empty(_journal);
    }
}
