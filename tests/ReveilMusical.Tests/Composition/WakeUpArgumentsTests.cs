using ReveilMusical.Domain;
using ReveilMusical.Host;

namespace ReveilMusical.Tests.Composition;

public class WakeUpArgumentsTests
{
    [Fact]
    public void TryParse_SansArgument_UtiliseLesValeursParDefaut()
    {
        Assert.True(WakeUpArguments.TryParse([], out var request, out var error));

        Assert.Equal(new WakeUpRequest("alice", DayOfWeek.Monday, WeatherType.Soleil), request);
        Assert.Empty(error);
    }

    [Theory]
    [InlineData("vendredi", "PLUIE", DayOfWeek.Friday, WeatherType.Pluie)]
    [InlineData("Dimanche", "neige", DayOfWeek.Sunday, WeatherType.Neige)]
    [InlineData("MARDI", "Nuageux", DayOfWeek.Tuesday, WeatherType.Nuageux)]
    [InlineData("lundi", "SOLEIL", DayOfWeek.Monday, WeatherType.Soleil)]
    [InlineData("mercredi", "soleil", DayOfWeek.Wednesday, WeatherType.Soleil)]
    [InlineData("jeudi", "pluie", DayOfWeek.Thursday, WeatherType.Pluie)]
    [InlineData("samedi", "neige", DayOfWeek.Saturday, WeatherType.Neige)]
    public void TryParse_JourEtMeteoEnFrancais_SontReconnusSansTenirCompteDeLaCasse(
        string jour, string meteo, DayOfWeek jourAttendu, WeatherType meteoAttendue)
    {
        Assert.True(WakeUpArguments.TryParse(["bob", jour, meteo], out var request, out _));

        Assert.Equal(new WakeUpRequest("bob", jourAttendu, meteoAttendue), request);
    }

    [Fact]
    public void TryParse_ArgumentsDeConfiguration_SontIgnores()
    {
        Assert.True(WakeUpArguments.TryParse(
            ["--Music:Providers:0=musicbrainz", "chloe", "jeudi"], out var request, out _));

        Assert.Equal(new WakeUpRequest("chloe", DayOfWeek.Thursday, WeatherType.Soleil), request);
    }

    [Fact]
    public void TryParse_JourInconnu_RenvoieFalseAvecLUsage()
    {
        Assert.False(WakeUpArguments.TryParse(["bob", "monday"], out _, out var error));

        Assert.Contains("monday", error);
        Assert.Contains("Usage", error);
    }

    [Fact]
    public void TryParse_MeteoInconnue_RenvoieFalseAvecLUsage()
    {
        Assert.False(WakeUpArguments.TryParse(["bob", "lundi", "grêle"], out _, out var error));

        Assert.Contains("grêle", error);
    }
}
