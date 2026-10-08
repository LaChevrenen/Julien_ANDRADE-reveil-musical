using ReveilMusical.Application;
using ReveilMusical.Domain;

namespace ReveilMusical.Tests.Fakes;

/// <summary>Canal programmable : réussit ou lève. Garde les messages envoyés.</summary>
public sealed class FakeNotificationChannel : INotificationChannel
{
    private readonly List<string>? _journal;

    public FakeNotificationChannel(string id, bool fails = false, List<string>? journal = null)
    {
        Id = new ChannelId(id);
        Fails = fails;
        _journal = journal;
    }

    public ChannelId Id { get; }

    public bool Fails { get; set; }

    public Action? OnSend { get; set; }

    public List<(string UserId, WakeUpMessage Message)> Sent { get; } = [];

    public int Attempts { get; private set; }

    public Task SendAsync(string userId, WakeUpMessage message, CancellationToken ct)
    {
        Attempts++;
        _journal?.Add(Id.Value);
        OnSend?.Invoke();
        ct.ThrowIfCancellationRequested();

        if (Fails)
        {
            throw new InvalidOperationException($"Canal {Id} en panne");
        }

        Sent.Add((userId, message));
        return Task.CompletedTask;
    }
}
