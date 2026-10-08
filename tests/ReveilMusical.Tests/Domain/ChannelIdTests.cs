using ReveilMusical.Domain;

namespace ReveilMusical.Tests.Domain;

public class ChannelIdTests
{
    [Theory]
    [InlineData("EMAIL", "email")]
    [InlineData("  Sms ", "sms")]
    [InlineData("push", "push")]
    public void Constructeur_ValeurNonNormalisee_NormaliseEnMinusculesSansEspaces(string brut, string attendu)
    {
        Assert.Equal(attendu, new ChannelId(brut).Value);
    }

    [Fact]
    public void Egalite_MemeCanalCasseDifferente_SontEgaux()
    {
        Assert.Equal(new ChannelId("WhatsApp"), new ChannelId(" whatsapp "));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructeur_ValeurVide_Leve(string brut)
    {
        Assert.Throws<ArgumentException>(() => new ChannelId(brut));
    }

    [Fact]
    public void ToString_RenvoieLaValeur()
    {
        Assert.Equal("sms", new ChannelId("SMS").ToString());
    }
}
