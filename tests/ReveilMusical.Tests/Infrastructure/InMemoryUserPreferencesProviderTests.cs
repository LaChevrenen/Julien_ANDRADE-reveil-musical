using ReveilMusical.Application;
using ReveilMusical.Domain;
using ReveilMusical.Infrastructure.Preferences;

namespace ReveilMusical.Tests.Infrastructure;

public class InMemoryUserPreferencesProviderTests
{
    private readonly InMemoryUserPreferencesProvider _provider = new();

    [Theory]
    [InlineData("alice", "email")]
    [InlineData("bob", "sms")]
    [InlineData("chloe", "push")]
    [InlineData(" ALICE ", "email")]
    public async Task GetAsync_UtilisateurDeDemo_RenvoieSonCanalPrefereEtUnMorceauDeSecours(string userId, string canal)
    {
        var prefs = await _provider.GetAsync(userId, CancellationToken.None);

        Assert.Equal(new ChannelId(canal), prefs.PreferredChannel);
        Assert.False(string.IsNullOrWhiteSpace(prefs.FallbackTrack));
        Assert.NotEmpty(prefs.TrackByWeather);
    }

    [Fact]
    public async Task GetAsync_Alice_CouvreToutesLesMeteos()
    {
        var prefs = await _provider.GetAsync("alice", CancellationToken.None);

        Assert.All(Enum.GetValues<WeatherType>(), meteo => Assert.True(prefs.TrackByWeather.ContainsKey(meteo)));
    }

    [Fact]
    public async Task GetAsync_Bob_NeCouvrePasLaNeige()
    {
        var prefs = await _provider.GetAsync("bob", CancellationToken.None);

        Assert.False(prefs.TrackByWeather.ContainsKey(WeatherType.Neige));
    }

    [Fact]
    public async Task GetAsync_UtilisateurInconnu_LeveUserNotFoundException()
    {
        var ex = await Assert.ThrowsAsync<UserNotFoundException>(() => _provider.GetAsync("zoe", CancellationToken.None));

        Assert.Equal("zoe", ex.UserId);
    }

    [Fact]
    public async Task GetAsync_JetonAnnule_PropageLAnnulation()
    {
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _provider.GetAsync("alice", new CancellationToken(canceled: true)));
    }
}
