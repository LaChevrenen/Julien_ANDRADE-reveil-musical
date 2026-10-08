using ReveilMusical.Application;
using ReveilMusical.Domain;
using ReveilMusical.Infrastructure.Notifications;

namespace ReveilMusical.Tests.Infrastructure;

/// <summary>Contrat commun à tout canal : envoi OK, échec du client → lève, annulation propagée.</summary>
public abstract class NotificationChannelContractTests
{
    protected static readonly WakeUpMessage Message = new("Bonjour <alice> ! Il pleut.");

    protected abstract string ExpectedId { get; }

    /// <summary>Ce que le client simulé a reçu, rendu en texte.</summary>
    protected abstract IReadOnlyList<string> Deliveries { get; }

    protected abstract INotificationChannel CreateChannel(bool clientFails);

    [Fact]
    public void Id_EstLIdentifiantNormaliseDuCanal()
    {
        Assert.Equal(new ChannelId(ExpectedId), CreateChannel(clientFails: false).Id);
    }

    [Fact]
    public async Task SendAsync_ClientDisponible_TransmetLeMessageAuDestinataireDeLUtilisateur()
    {
        await CreateChannel(clientFails: false).SendAsync("alice", Message, CancellationToken.None);

        var delivery = Assert.Single(Deliveries);
        Assert.Contains("alice", delivery);
        Assert.Contains("Il pleut.", delivery);
    }

    [Fact]
    public async Task SendAsync_ClientEnEchec_Leve()
    {
        var channel = CreateChannel(clientFails: true);

        await Assert.ThrowsAnyAsync<Exception>(() => channel.SendAsync("alice", Message, CancellationToken.None));
    }

    [Fact]
    public async Task SendAsync_JetonAnnule_PropageSansEnvoyer()
    {
        var channel = CreateChannel(clientFails: false);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => channel.SendAsync("alice", Message, new CancellationToken(canceled: true)));
        Assert.Empty(Deliveries);
    }
}

public class EmailChannelTests : NotificationChannelContractTests
{
    private readonly FakeEmailClient _client = new();

    protected override string ExpectedId => "email";

    protected override IReadOnlyList<string> Deliveries => _client.Mails;

    protected override INotificationChannel CreateChannel(bool clientFails)
    {
        _client.Fails = clientFails;
        return new EmailChannel(_client);
    }

    [Fact]
    public async Task SendAsync_Message_EstEncodeEnHtmlAvecUnObjet()
    {
        await CreateChannel(clientFails: false).SendAsync("alice", Message, CancellationToken.None);

        Assert.Equal(
            "alice@reveil-musical.example|Votre réveil musical|<p>Bonjour &lt;alice&gt; ! Il pleut.</p>",
            Assert.Single(_client.Mails));
    }

    private sealed class FakeEmailClient : IEmailClient
    {
        public bool Fails { get; set; }

        public List<string> Mails { get; } = [];

        public Task SendMailAsync(string to, string subject, string htmlBody)
        {
            if (Fails)
            {
                throw new IOException("SMTP indisponible");
            }

            Mails.Add($"{to}|{subject}|{htmlBody}");
            return Task.CompletedTask;
        }
    }
}

public class SmsChannelTests : NotificationChannelContractTests
{
    private readonly FakeSmsGateway _gateway = new();

    protected override string ExpectedId => "sms";

    protected override IReadOnlyList<string> Deliveries => _gateway.Messages.Select(m => $"alice:{m.Phone}:{m.Text}").ToList();

    protected override INotificationChannel CreateChannel(bool clientFails)
    {
        _gateway.Fails = clientFails;
        return new SmsChannel(_gateway);
    }

    [Fact]
    public async Task SendAsync_MemeUtilisateur_MemeNumeroStableAuFormatInternational()
    {
        var channel = CreateChannel(clientFails: false);

        await channel.SendAsync("alice", Message, CancellationToken.None);
        await channel.SendAsync("alice", Message, CancellationToken.None);
        await channel.SendAsync("bob", Message, CancellationToken.None);

        var phones = _gateway.Messages.Select(m => m.Phone).ToList();
        Assert.Equal(phones[0], phones[1]);
        Assert.NotEqual(phones[0], phones[2]);
        Assert.All(phones, p => Assert.Matches(@"^\+336\d{8}$", p));
    }

    private sealed class FakeSmsGateway : ISmsGateway
    {
        public bool Fails { get; set; }

        public List<(string Phone, string Text)> Messages { get; } = [];

        public void SendSms(string phoneNumber, string text)
        {
            if (Fails)
            {
                throw new TimeoutException("Passerelle SMS muette");
            }

            Messages.Add((phoneNumber, text));
        }
    }
}

public class PushChannelTests : NotificationChannelContractTests
{
    private readonly FakePushService _service = new();

    protected override string ExpectedId => "push";

    protected override IReadOnlyList<string> Deliveries =>
        _service.Payloads.Select(p => $"{p.DeviceToken}|{p.Title}|{p.Body}|{p.Data["userId"]}").ToList();

    protected override INotificationChannel CreateChannel(bool clientFails)
    {
        _service.Accepts = !clientFails;
        return new PushChannel(_service);
    }

    [Fact]
    public async Task SendAsync_ServiceRenvoieFalse_LAdaptateurLeve()
    {
        var channel = CreateChannel(clientFails: true);

        var ex = await Assert.ThrowsAsync<ChannelDeliveryException>(
            () => channel.SendAsync("alice", Message, CancellationToken.None));
        Assert.Contains("alice", ex.Message);
    }

    [Fact]
    public async Task SendAsync_ServiceLeve_LExceptionEstPropagee()
    {
        _service.Throws = true;

        await Assert.ThrowsAsync<HttpRequestException>(
            () => new PushChannel(_service).SendAsync("alice", Message, CancellationToken.None));
    }

    [Fact]
    public async Task SendAsync_Payload_PorteJetonTitreCorpsEtDonnees()
    {
        await CreateChannel(clientFails: false).SendAsync("alice", Message, CancellationToken.None);

        var payload = Assert.Single(_service.Payloads);
        Assert.Equal("device-alice", payload.DeviceToken);
        Assert.Equal("Réveil musical", payload.Title);
        Assert.Equal(Message.Text, payload.Body);
        Assert.Equal("alice", payload.Data["userId"]);
    }

    private sealed class FakePushService : IPushService
    {
        public bool Accepts { get; set; } = true;

        public bool Throws { get; set; }

        public List<PushPayload> Payloads { get; } = [];

        public Task<bool> PushAsync(PushPayload payload)
        {
            if (Throws)
            {
                throw new HttpRequestException("Service push injoignable");
            }

            if (Accepts)
            {
                Payloads.Add(payload);
            }

            return Task.FromResult(Accepts);
        }
    }
}
