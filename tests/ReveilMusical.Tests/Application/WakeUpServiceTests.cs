using Microsoft.Extensions.Logging;
using ReveilMusical.Application;
using ReveilMusical.Domain;
using ReveilMusical.Tests.Fakes;

namespace ReveilMusical.Tests.Application;

public class WakeUpServiceTests
{
    private static readonly UserPreferences Prefs = new(
        new Dictionary<WeatherType, string>
        {
            [WeatherType.Soleil] = "Walking on Sunshine",
            [WeatherType.Pluie] = "Purple Rain",
        },
        "Here Comes the Sun",
        new ChannelId("email"));

    private readonly FakeTrackResolver _resolver = new();
    private readonly FakeNotificationDispatcher _dispatcher = new();
    private readonly ListLogger<WakeUpService> _logger = new();

    private WakeUpService Build(IUserPreferencesProvider? preferences = null) => new(
        preferences ?? new FakeUserPreferencesProvider(Prefs),
        new TrackSelector(),
        _resolver,
        new WakeUpMessageComposer(),
        _dispatcher,
        _logger);

    [Fact]
    public async Task SendWakeUpAsync_ToutDisponible_EnvoieLeMorceauDeLaMeteoSurLeCanalPrefere()
    {
        var result = await Build().SendWakeUpAsync(new WakeUpRequest("alice", DayOfWeek.Monday, WeatherType.Pluie));

        Assert.Equal("Purple Rain", result.Track.Title);
        Assert.Equal(new ChannelId("email"), result.ChannelUsed);
        Assert.False(result.Degraded);
        Assert.Empty(_logger.Entries);
    }

    [Fact]
    public async Task SendWakeUpAsync_Message_UtiliseLeJourLaMeteoEtLeMorceau()
    {
        await Build().SendWakeUpAsync(new WakeUpRequest("alice", DayOfWeek.Friday, WeatherType.Soleil));

        var (userId, preferred, message) = Assert.Single(_dispatcher.Dispatched);
        Assert.Equal("alice", userId);
        Assert.Equal(new ChannelId("email"), preferred);
        Assert.Contains("vendredi", message.Text);
        Assert.Contains("il fait soleil", message.Text);
        Assert.Contains("Walking on Sunshine", message.Text);
    }

    [Fact]
    public async Task SendWakeUpAsync_MeteoNonCouverte_DemandeLeMorceauDeSecoursSansEtreDegrade()
    {
        var result = await Build().SendWakeUpAsync(new WakeUpRequest("alice", DayOfWeek.Monday, WeatherType.Neige));

        Assert.Equal(["Here Comes the Sun"], _resolver.Queries);
        Assert.False(result.Degraded);
    }

    [Fact]
    public async Task SendWakeUpAsync_RepliLocal_EstDegrade()
    {
        _resolver.UsedLocalFallback = true;

        var result = await Build().SendWakeUpAsync(new WakeUpRequest("alice", DayOfWeek.Monday, WeatherType.Pluie));

        Assert.True(result.Degraded);
        Assert.Equal("local", result.Track.Source);
        Assert.Equal(new ChannelId("email"), result.ChannelUsed);
        Assert.Equal(1, _logger.Count(LogLevel.Warning));
    }

    [Fact]
    public async Task SendWakeUpAsync_CanalDeRepliUtilise_EstDegrade()
    {
        _dispatcher.ChannelToUse = new ChannelId("sms");

        var result = await Build().SendWakeUpAsync(new WakeUpRequest("alice", DayOfWeek.Monday, WeatherType.Pluie));

        Assert.True(result.Degraded);
        Assert.Equal(new ChannelId("sms"), result.ChannelUsed);
    }

    [Fact]
    public async Task SendWakeUpAsync_TousLesCanauxEnPanne_RenvoieCanalNullSansLever()
    {
        _dispatcher.AllFail = true;

        var result = await Build().SendWakeUpAsync(new WakeUpRequest("alice", DayOfWeek.Monday, WeatherType.Pluie));

        Assert.Null(result.ChannelUsed);
        Assert.True(result.Degraded);
        Assert.Equal("Purple Rain", result.Track.Title);
    }

    [Fact]
    public async Task SendWakeUpAsync_PreferencesEnPanneDerriereLeDecorateur_EnvoieAvecLesDefauts()
    {
        var preferences = new DefaultingUserPreferencesProvider(
            FakeUserPreferencesProvider.Throwing(new TimeoutException("service de préférences muet")),
            new WakeUpDefaults("Here Comes the Sun"),
            new NotificationPolicy([new ChannelId("push")], new ChannelId("push")),
            new ListLogger<DefaultingUserPreferencesProvider>());

        var result = await Build(preferences).SendWakeUpAsync(new WakeUpRequest("zoe", DayOfWeek.Monday, WeatherType.Pluie));

        Assert.Equal("Here Comes the Sun", result.Track.Title);
        Assert.Equal(new ChannelId("push"), result.ChannelUsed);
    }

    [Fact]
    public async Task SendWakeUpAsync_JetonAnnule_PropageSansNotifier()
    {
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Build().SendWakeUpAsync(
            new WakeUpRequest("alice", DayOfWeek.Monday, WeatherType.Pluie),
            new CancellationToken(canceled: true)));
        Assert.Empty(_dispatcher.Dispatched);
    }
}
