using ReveilMusical.Application;
using ReveilMusical.Domain;

namespace ReveilMusical.Tests.Fakes;

public sealed class FakeUserPreferencesProvider : IUserPreferencesProvider
{
    private readonly Func<string, UserPreferences> _behavior;

    public FakeUserPreferencesProvider(UserPreferences preferences)
        : this(_ => preferences)
    {
    }

    public FakeUserPreferencesProvider(Func<string, UserPreferences> behavior)
    {
        _behavior = behavior;
    }

    public int Calls { get; private set; }

    public static FakeUserPreferencesProvider Throwing(Exception exception) => new(_ => throw exception);

    public Task<UserPreferences> GetAsync(string userId, CancellationToken ct)
    {
        Calls++;
        return Task.FromResult(_behavior(userId));
    }
}

public sealed class FakeTrackResolver : ITrackResolver
{
    public bool UsedLocalFallback { get; set; }

    public List<string> Queries { get; } = [];

    public Task<TrackResolution> ResolveAsync(string query, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Queries.Add(query);
        var source = UsedLocalFallback ? "local" : "itunes";
        return Task.FromResult(new TrackResolution(new Track(query, "Artiste", source), UsedLocalFallback));
    }
}

public sealed class FakeNotificationDispatcher : INotificationDispatcher
{
    /// <summary>Canal renvoyé ; <c>null</c> = utilise le canal préféré. Ignoré si <see cref="AllFail"/>.</summary>
    public ChannelId? ChannelToUse { get; set; }

    public bool AllFail { get; set; }

    public List<(string UserId, ChannelId Preferred, WakeUpMessage Message)> Dispatched { get; } = [];

    public Task<ChannelId?> DispatchAsync(string userId, ChannelId preferred, WakeUpMessage message, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Dispatched.Add((userId, preferred, message));
        return Task.FromResult(AllFail ? null : ChannelToUse ?? preferred);
    }
}
