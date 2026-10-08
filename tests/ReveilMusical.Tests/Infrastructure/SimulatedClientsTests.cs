using Microsoft.Extensions.Logging;
using ReveilMusical.Infrastructure.Notifications;
using ReveilMusical.Tests.Fakes;

namespace ReveilMusical.Tests.Infrastructure;

public class SimulatedClientsTests
{
    [Fact]
    public async Task ConsoleEmailClient_SendMailAsync_EcritLeMailSurLaSortieFournie()
    {
        using var output = new StringWriter();

        await new ConsoleEmailClient(output).SendMailAsync("alice@exemple.fr", "Objet", "<p>Bonjour</p>");

        var ligne = output.ToString();
        Assert.Contains("[EMAIL]", ligne);
        Assert.Contains("alice@exemple.fr", ligne);
        Assert.Contains("Objet", ligne);
        Assert.Contains("<p>Bonjour</p>", ligne);
    }

    [Fact]
    public void LogSmsGateway_SendSms_JournaliseLeSms()
    {
        var logger = new ListLogger<LogSmsGateway>();

        new LogSmsGateway(logger).SendSms("+33600000000", "Bonjour");

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal("[SMS] +33600000000 : Bonjour", entry.Message);
    }

    [Fact]
    public async Task LogPushService_PushAsync_JournaliseEtAccepte()
    {
        var logger = new ListLogger<LogPushService>();
        var payload = new PushPayload("device-alice", "Titre", "Corps", new Dictionary<string, string>());

        var accepted = await new LogPushService(logger).PushAsync(payload);

        Assert.True(accepted);
        Assert.Equal("[PUSH] device-alice : Titre — Corps", Assert.Single(logger.Entries).Message);
    }
}
