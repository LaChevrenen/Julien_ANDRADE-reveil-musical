using ReveilMusical.Application;
using ReveilMusical.Domain;

namespace ReveilMusical.Tests.Application;

public class TrackSelectorTests
{
    private static readonly UserPreferences Prefs = new(
        new Dictionary<WeatherType, string>
        {
            [WeatherType.Soleil] = "Walking on Sunshine",
            [WeatherType.Pluie] = "Purple Rain",
            [WeatherType.Nuageux] = "  ",
        },
        "Here Comes the Sun",
        new ChannelId("email"));

    private readonly TrackSelector _selector = new();

    [Theory]
    [InlineData(WeatherType.Soleil, "Walking on Sunshine")]
    [InlineData(WeatherType.Pluie, "Purple Rain")]
    public void Select_MeteoCouverte_RenvoieLeTitreChoisi(WeatherType meteo, string attendu)
    {
        Assert.Equal(attendu, _selector.Select(Prefs, meteo));
    }

    [Fact]
    public void Select_MeteoNonCouverte_RenvoieLeMorceauDeSecours()
    {
        Assert.Equal("Here Comes the Sun", _selector.Select(Prefs, WeatherType.Neige));
    }

    [Fact]
    public void Select_TitreVidePourLaMeteo_RenvoieLeMorceauDeSecours()
    {
        Assert.Equal("Here Comes the Sun", _selector.Select(Prefs, WeatherType.Nuageux));
    }
}
