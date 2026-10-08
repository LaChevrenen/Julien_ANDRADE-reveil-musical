using ReveilMusical.Domain;

namespace ReveilMusical.Tests.Domain;

public class ModelsTests
{
    [Fact]
    public void Track_MemesValeurs_SontEgaux()
    {
        Assert.Equal(new Track("Été", "Édith", "local"), new Track("Été", "Édith", "local"));
    }

    [Fact]
    public void WeatherType_CouvreLesQuatreMeteosDeLEnonce()
    {
        Assert.Equal(
            ["Soleil", "Pluie", "Neige", "Nuageux"],
            Enum.GetNames<WeatherType>());
    }

    [Fact]
    public void WakeUpRequest_ConserveSesEntrees()
    {
        var request = new WakeUpRequest("alice", DayOfWeek.Monday, WeatherType.Pluie);

        Assert.Equal("alice", request.UserId);
        Assert.Equal(DayOfWeek.Monday, request.Day);
        Assert.Equal(WeatherType.Pluie, request.Weather);
    }

    [Fact]
    public void UserPreferences_ConserveSesEntrees()
    {
        var parMeteo = new Dictionary<WeatherType, string> { [WeatherType.Soleil] = "Walking on Sunshine" };

        var prefs = new UserPreferences(parMeteo, "Here Comes the Sun", new ChannelId("email"));

        Assert.Equal("Walking on Sunshine", prefs.TrackByWeather[WeatherType.Soleil]);
        Assert.Equal("Here Comes the Sun", prefs.FallbackTrack);
        Assert.Equal("email", prefs.PreferredChannel.Value);
        Assert.Equal("Bonjour", new WakeUpMessage("Bonjour").Text);
    }
}
