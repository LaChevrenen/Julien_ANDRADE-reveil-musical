using System.Globalization;
using ReveilMusical.Application;
using ReveilMusical.Domain;

namespace ReveilMusical.Tests.Application;

public class WakeUpMessageComposerTests
{
    private static readonly Track Morceau = new("Purple Rain", "Prince", "itunes");

    private readonly WakeUpMessageComposer _composer = new();

    [Theory]
    [InlineData(DayOfWeek.Monday, "lundi")]
    [InlineData(DayOfWeek.Tuesday, "mardi")]
    [InlineData(DayOfWeek.Wednesday, "mercredi")]
    [InlineData(DayOfWeek.Thursday, "jeudi")]
    [InlineData(DayOfWeek.Friday, "vendredi")]
    [InlineData(DayOfWeek.Saturday, "samedi")]
    [InlineData(DayOfWeek.Sunday, "dimanche")]
    public void Compose_ChaqueJour_ContientLeJourEnFrancais(DayOfWeek jour, string attendu)
    {
        Assert.Contains(attendu, _composer.Compose(jour, WeatherType.Soleil, Morceau).Text);
    }

    [Theory]
    [InlineData(WeatherType.Soleil, "il fait soleil")]
    [InlineData(WeatherType.Pluie, "il pleut")]
    [InlineData(WeatherType.Neige, "il neige")]
    [InlineData(WeatherType.Nuageux, "nuageux")]
    public void Compose_ChaqueMeteo_ContientLaMeteoEnFrancais(WeatherType meteo, string attendu)
    {
        Assert.Contains(attendu, _composer.Compose(DayOfWeek.Monday, meteo, Morceau).Text);
    }

    [Fact]
    public void Compose_Morceau_ContientTitreEtArtiste()
    {
        var texte = _composer.Compose(DayOfWeek.Friday, WeatherType.Pluie, Morceau).Text;

        Assert.Equal("Bonjour ! Nous sommes vendredi et il pleut. Votre morceau du réveil : « Purple Rain » de Prince.", texte);
    }

    [Fact]
    public void Compose_ArtisteVide_NAfficheQueLeTitre()
    {
        var texte = _composer.Compose(DayOfWeek.Friday, WeatherType.Pluie, new Track("Purple Rain", "", "local")).Text;

        Assert.EndsWith("« Purple Rain ».", texte);
    }

    [Fact]
    public void Compose_ValeursHorsEnum_NeLevePasEtContientLeMorceau()
    {
        var texte = _composer.Compose((DayOfWeek)42, (WeatherType)42, Morceau).Text;

        Assert.Contains("Purple Rain", texte);
    }

    [Fact]
    public void Compose_CultureAnglaise_ResteEnFrancais()
    {
        var precedente = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-US");

            Assert.Contains("lundi", _composer.Compose(DayOfWeek.Monday, WeatherType.Neige, Morceau).Text);
        }
        finally
        {
            CultureInfo.CurrentCulture = precedente;
        }
    }
}
