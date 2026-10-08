using Microsoft.Extensions.Logging;
using ReveilMusical.Application;
using ReveilMusical.Domain;
using ReveilMusical.Tests.Fakes;

namespace ReveilMusical.Tests.Application;

public class DefaultingUserPreferencesProviderTests
{
    private static readonly UserPreferences Connues = new(
        new Dictionary<WeatherType, string> { [WeatherType.Pluie] = "Purple Rain" },
        "Eye of the Tiger",
        new ChannelId("sms"));

    private readonly ListLogger<DefaultingUserPreferencesProvider> _logger = new();

    private DefaultingUserPreferencesProvider Decorate(IUserPreferencesProvider inner) => new(
        inner,
        new WakeUpDefaults("Here Comes the Sun"),
        new NotificationPolicy([new ChannelId("push"), new ChannelId("sms")], new ChannelId("push")),
        _logger);

    [Fact]
    public async Task GetAsync_ServiceDisponible_RenvoieLesPreferencesDeLUtilisateur()
    {
        var prefs = await Decorate(new FakeUserPreferencesProvider(Connues)).GetAsync("bob", CancellationToken.None);

        Assert.Same(Connues, prefs);
        Assert.Empty(_logger.Entries);
    }

    [Fact]
    public async Task GetAsync_UtilisateurInconnu_RenvoieLesPreferencesParDefaut()
    {
        var inner = new FakeUserPreferencesProvider(id => throw new UserNotFoundException(id));

        var prefs = await Decorate(inner).GetAsync("zoe", CancellationToken.None);

        Assert.Empty(prefs.TrackByWeather);
        Assert.Equal("Here Comes the Sun", prefs.FallbackTrack);
        Assert.Equal(new ChannelId("push"), prefs.PreferredChannel);
        Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("zoe"));
    }

    [Fact]
    public async Task GetAsync_ServiceEnPanne_RenvoieLesPreferencesParDefaut()
    {
        var inner = FakeUserPreferencesProvider.Throwing(new HttpRequestException("503"));

        var prefs = await Decorate(inner).GetAsync("alice", CancellationToken.None);

        Assert.Equal("Here Comes the Sun", prefs.FallbackTrack);
    }

    [Fact]
    public async Task GetAsync_TimeoutDuService_EstUnePanneEtNonUneAnnulation()
    {
        var inner = FakeUserPreferencesProvider.Throwing(new TaskCanceledException("timeout"));

        var prefs = await Decorate(inner).GetAsync("alice", CancellationToken.None);

        Assert.Equal(new ChannelId("push"), prefs.PreferredChannel);
    }

    [Fact]
    public async Task GetAsync_AnnulationDemandee_EstPropagee()
    {
        using var cts = new CancellationTokenSource();
        var inner = new FakeUserPreferencesProvider(_ =>
        {
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Decorate(inner).GetAsync("alice", cts.Token));
    }

    [Fact]
    public async Task GetAsync_JetonDejaAnnule_NAppellePasLeService()
    {
        var inner = new FakeUserPreferencesProvider(Connues);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Decorate(inner).GetAsync("alice", new CancellationToken(canceled: true)));
        Assert.Equal(0, inner.Calls);
    }

    [Fact]
    public void UserNotFoundException_PorteLIdentifiant()
    {
        var ex = new UserNotFoundException("zoe");

        Assert.Equal("zoe", ex.UserId);
        Assert.Contains("zoe", ex.Message);
    }
}
