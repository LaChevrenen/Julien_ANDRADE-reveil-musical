using Microsoft.Extensions.Logging;
using ReveilMusical.Application;
using ReveilMusical.Domain;
using ReveilMusical.Tests.Fakes;

namespace ReveilMusical.Tests.Application;

public class FallbackNotificationDispatcherTests
{
    private static readonly WakeUpMessage Message = new("Bonjour !");

    private readonly List<string> _journal = [];
    private readonly ListLogger<FallbackNotificationDispatcher> _logger = new();
    private readonly FakeNotificationChannel _email;
    private readonly FakeNotificationChannel _sms;
    private readonly FakeNotificationChannel _push;

    public FallbackNotificationDispatcherTests()
    {
        _email = new FakeNotificationChannel("email", journal: _journal);
        _sms = new FakeNotificationChannel("sms", journal: _journal);
        _push = new FakeNotificationChannel("push", journal: _journal);
    }

    private FallbackNotificationDispatcher Build(params string[] fallbackOrder) => new(
        [_email, _sms, _push],
        new NotificationPolicy(fallbackOrder.Select(c => new ChannelId(c)).ToList(), new ChannelId("push")),
        _logger);

    private Task<ChannelId?> Dispatch(FallbackNotificationDispatcher dispatcher, string preferred, CancellationToken ct = default) =>
        dispatcher.DispatchAsync("alice", new ChannelId(preferred), Message, ct);

    [Fact]
    public async Task DispatchAsync_CanalPrefereDisponible_LUtiliseSeul()
    {
        var used = await Dispatch(Build("push", "sms", "email"), "email");

        Assert.Equal(new ChannelId("email"), used);
        Assert.Equal(["email"], _journal);
        Assert.Equal(("alice", Message), Assert.Single(_email.Sent));
    }

    [Fact]
    public async Task DispatchAsync_CanalPrefereEnPanne_SuitLOrdreDeRepli()
    {
        _email.Fails = true;
        _push.Fails = true;

        var used = await Dispatch(Build("push", "sms", "email"), "email");

        Assert.Equal(new ChannelId("sms"), used);
        Assert.Equal(["email", "push", "sms"], _journal);
    }

    [Fact]
    public async Task DispatchAsync_CanalPrefereDansLOrdreDeRepli_NEstPasEssayeDeuxFois()
    {
        _sms.Fails = true;
        _push.Fails = true;
        _email.Fails = true;

        await Dispatch(Build("push", "sms", "email"), "sms");

        Assert.Equal(["sms", "push", "email"], _journal);
    }

    [Fact]
    public async Task DispatchAsync_CanalPrefereInconnu_EstUnEchecCommeUnAutre()
    {
        var used = await Dispatch(Build("push", "sms", "email"), "whatsapp");

        Assert.Equal(new ChannelId("push"), used);
        Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("whatsapp"));
    }

    [Fact]
    public async Task DispatchAsync_CanalInconnuDansLOrdreDeRepli_EstIgnore()
    {
        _email.Fails = true;

        var used = await Dispatch(Build("appel-vocal", "sms"), "email");

        Assert.Equal(new ChannelId("sms"), used);
    }

    [Fact]
    public async Task DispatchAsync_TousLesCanauxEnPanne_RenvoieNullEtJournaliseEnCritique()
    {
        _email.Fails = true;
        _sms.Fails = true;
        _push.Fails = true;

        var used = await Dispatch(Build("push", "sms", "email"), "email");

        Assert.Null(used);
        Assert.Equal(1, _logger.Count(LogLevel.Critical));
    }

    [Fact]
    public async Task DispatchAsync_AucunCanalEnregistre_RenvoieNullSansLever()
    {
        var dispatcher = new FallbackNotificationDispatcher(
            [],
            new NotificationPolicy([new ChannelId("push")], new ChannelId("push")),
            _logger);

        Assert.Null(await Dispatch(dispatcher, "email"));
        Assert.Equal(1, _logger.Count(LogLevel.Critical));
    }

    [Fact]
    public async Task DispatchAsync_OrdreDeRepliVide_NEssaieQueLePrefere()
    {
        _email.Fails = true;

        Assert.Null(await Dispatch(Build(), "email"));
        Assert.Equal(["email"], _journal);
    }

    [Fact]
    public async Task DispatchAsync_AnnulationPendantLEnvoi_EstPropageeSansEssayerLesAutres()
    {
        using var cts = new CancellationTokenSource();
        _email.OnSend = cts.Cancel;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Dispatch(Build("push", "sms"), "email", cts.Token));
        Assert.Equal(["email"], _journal);
    }

    [Fact]
    public async Task DispatchAsync_JetonDejaAnnule_NEnvoieRien()
    {
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Dispatch(Build("push"), "email", new CancellationToken(canceled: true)));
        Assert.Empty(_journal);
    }
}
