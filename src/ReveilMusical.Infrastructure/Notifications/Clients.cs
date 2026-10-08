using Microsoft.Extensions.Logging;

namespace ReveilMusical.Infrastructure.Notifications;

// Trois clients simulés aux interfaces volontairement différentes, comme le seraient
// trois SDK tiers. Aucun envoi réel : écriture console ou journal.

internal interface IEmailClient
{
    Task SendMailAsync(string to, string subject, string htmlBody);
}

internal interface ISmsGateway
{
    void SendSms(string phoneNumber, string text);
}

internal interface IPushService
{
    Task<bool> PushAsync(PushPayload payload);
}

internal sealed record PushPayload(string DeviceToken, string Title, string Body, IReadOnlyDictionary<string, string> Data);

internal sealed class ConsoleEmailClient : IEmailClient
{
    private readonly TextWriter _output;

    public ConsoleEmailClient(TextWriter output)
    {
        _output = output;
    }

    public Task SendMailAsync(string to, string subject, string htmlBody)
    {
        return _output.WriteLineAsync($"[EMAIL] À : {to} | Objet : {subject} | Corps : {htmlBody}");
    }
}

internal sealed class LogSmsGateway : ISmsGateway
{
    private readonly ILogger<LogSmsGateway> _logger;

    public LogSmsGateway(ILogger<LogSmsGateway> logger)
    {
        _logger = logger;
    }

    public void SendSms(string phoneNumber, string text)
    {
        _logger.LogInformation("[SMS] {PhoneNumber} : {Text}", phoneNumber, text);
    }
}

internal sealed class LogPushService : IPushService
{
    private readonly ILogger<LogPushService> _logger;

    public LogPushService(ILogger<LogPushService> logger)
    {
        _logger = logger;
    }

    public Task<bool> PushAsync(PushPayload payload)
    {
        _logger.LogInformation("[PUSH] {DeviceToken} : {Title} — {Body}", payload.DeviceToken, payload.Title, payload.Body);
        return Task.FromResult(true);
    }
}
