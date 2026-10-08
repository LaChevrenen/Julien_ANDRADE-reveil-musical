using System.Net;
using ReveilMusical.Application;
using ReveilMusical.Domain;

namespace ReveilMusical.Infrastructure.Notifications;

// Adaptateurs : ramènent chaque client à INotificationChannel.
// Hypothèse : pas d'annuaire de contacts, le destinataire est dérivé de userId.

internal sealed class ChannelDeliveryException : Exception
{
    public ChannelDeliveryException(string message)
        : base(message)
    {
    }
}

internal sealed class EmailChannel : INotificationChannel
{
    private const string Subject = "Votre réveil musical";

    private readonly IEmailClient _client;

    public EmailChannel(IEmailClient client)
    {
        _client = client;
    }

    public ChannelId Id { get; } = new("email");

    public async Task SendAsync(string userId, WakeUpMessage message, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var to = $"{userId}@reveil-musical.example";
        await _client.SendMailAsync(to, Subject, $"<p>{WebUtility.HtmlEncode(message.Text)}</p>");
    }
}

internal sealed class SmsChannel : INotificationChannel
{
    private readonly ISmsGateway _gateway;

    public SmsChannel(ISmsGateway gateway)
    {
        _gateway = gateway;
    }

    public ChannelId Id { get; } = new("sms");

    public Task SendAsync(string userId, WakeUpMessage message, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
        {
            return Task.FromCanceled(ct);
        }

        try
        {
            _gateway.SendSms(PhoneNumberOf(userId), message.Text);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            return Task.FromException(ex);
        }
    }

    /// <summary>Numéro fictif stable (string.GetHashCode varie d'un processus à l'autre).</summary>
    private static string PhoneNumberOf(string userId)
    {
        uint hash = 2166136261;
        foreach (var c in userId)
        {
            hash = (hash ^ c) * 16777619;
        }

        return $"+336{hash % 100_000_000:D8}";
    }
}

internal sealed class PushChannel : INotificationChannel
{
    private const string Title = "Réveil musical";

    private readonly IPushService _service;

    public PushChannel(IPushService service)
    {
        _service = service;
    }

    public ChannelId Id { get; } = new("push");

    public async Task SendAsync(string userId, WakeUpMessage message, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var payload = new PushPayload(
            $"device-{userId}",
            Title,
            message.Text,
            new Dictionary<string, string> { ["userId"] = userId });

        if (!await _service.PushAsync(payload))
        {
            throw new ChannelDeliveryException($"Le service push a refusé la notification pour {userId}.");
        }
    }
}
