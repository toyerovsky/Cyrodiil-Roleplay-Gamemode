using CyrodiilRP.Common.Chat;
using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Interactions;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Players;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Lore;

// The handshake: one offers to introduce themselves, the other answers with Y or N from the card at the top of the screen
// (or the lore card's Nearby). Only a yes changes anything, and then both know each other's names.
[Service]
internal sealed partial class LoreIntroductions(
    Asks asks,
    Introductions introductions,
    RpChat chat,
    CommandRegistry commands,
    InteractionMenu interactions,
    OnlinePlayers online,
    LoreTargets targets,
    Timers timers,
    SystemClock clock,
    RpConfig config)
{
    private const string IntroduceOption = "player.introduce";
    private const string AskJob = "lore.introduce_ask";

    private const int IntroduceOrder = 12;

    private readonly AskPacing _pacing = new(AskJob, timers, clock);

    // The offers waiting for an answer, the oldest first.
    private readonly List<PlayerAsk> _offers = [];

    public LoreOutcome Offer(OnlinePlayer actor, OnlinePlayer target)
    {
        if (actor == target)
            return new LoreOutcome(false, LoreText.IntroSelf);

        if (target.Character is null || !target.IsPlaying)
            return new LoreOutcome(false, LoreText.PlayerGone);

        if (introductions.AreMutual(actor, target))
            return new LoreOutcome(false, LoreText.IntroKnown(new ShownName(target)));

        if (Find(target, actor) is { } theirs)
            return Accept(theirs);

        if (Find(actor, target) is not null)
            return new LoreOutcome(false, LoreText.IntroWaiting(new ShownName(target)));

        if (_pacing.CountWaitSeconds(actor) is > 0 and var wait)
            return new LoreOutcome(false, LoreText.IntroCooldown(wait));

        var ask = new PlayerAsk(actor, target);
        _offers.Add(ask);
        _pacing.Start(ask, Expire);
        asks.Open(ask, AskKinds.Introduction, new AskAnswers(answered => Accept(answered), Decline));
        chat.SendTo(target, ChatLineKind.System, LoreText.IntroAsked(new ShownName(actor), AskPacing.TimeoutSeconds));
        return new LoreOutcome(true, LoreText.IntroOffered(new ShownName(target)));
    }

    public LoreOutcome AcceptFrom(OnlinePlayer asked, OnlinePlayer asker) =>
        Find(asker, asked) is { } ask ? Accept(ask) : new LoreOutcome(false, LoreText.IntroNotWaiting(new ShownName(asker)));

    // From one player, or the oldest offer when none is named.
    public LoreOutcome DeclineFrom(OnlinePlayer asked, OnlinePlayer? asker)
    {
        var ask = asker is null ? _offers.FirstOrDefault(offer => offer.Asked == asked) : Find(asker, asked);
        if (ask is null)
            return new LoreOutcome(false, asker is null ? LoreText.IntroNobodyWaiting : LoreText.IntroNotWaiting(new ShownName(asker)));

        Decline(ask);
        return new LoreOutcome(true, LoreText.IntroYouDeclined(new ShownName(ask.Asker)));
    }

    // The other no longer knows the forgetter's name.
    public LoreOutcome Forget(OnlinePlayer forgetter, OnlinePlayer other) =>
        introductions.Forget(other, forgetter)
            ? new LoreOutcome(true, LoreText.Forgot(new ShownName(other)))
            : new LoreOutcome(false, LoreText.ForgetUnknown(new ShownName(other)));

    public bool IsWaiting(OnlinePlayer asker, OnlinePlayer asked) => Find(asker, asked) is not null;

    private void Start()
    {
        commands.Add(new CommandSpec { Name = "introduce", Usage = LoreText.CmdIntroduce, Category = CommandCategory.Roleplay, Speaks = true }, Introduce);
        commands.Add(new CommandSpec { Name = "forget", Usage = LoreText.CmdForget, Category = CommandCategory.Roleplay, NeedsCharacter = true }, ForgetCommand);
        interactions.Add(new InteractionOption
        {
            Id = IntroduceOption,
            Label = LoreText.OptionIntroduce,
            Icon = WheelIcons.Hand,
            Order = IntroduceOrder,
            Speaks = true,
            Reach = config.Chat.Say,
            State = (actor, target) => introductions.AreMutual(actor, target)
                ? OptionState.Hidden
                : OptionState.OpenUnless(_pacing.CountWaitSeconds(actor) is > 0 and var wait && Find(target, actor) is null
                    ? LoreText.IntroCooldown(wait)
                    : null),
            Run = (actor, target) => Tell(actor, Offer(actor, target)),
        });
        online.Left += OnLeft;
    }

    private void Introduce(CommandCall call)
    {
        var target = call.Words.Count == 0 ? targets.FindInFront(call.Sender) : null;
        if (call.Words.Count > 0 && !call.TryTarget(0, out target))
            return;

        if (target is null)
        {
            call.Refuse(InteractionsText.NobodyInFront);
            return;
        }

        var outcome = Offer(call.Sender, target);
        if (outcome.Ok)
            call.Reply(outcome.Message);
        else
            call.Refuse(outcome.Message);
    }

    private void ForgetCommand(CommandCall call)
    {
        if (!call.TryTarget(0, out var target))
            return;

        if (target == call.Sender)
        {
            call.Refuse(LoreText.IntroSelf);
            return;
        }

        var outcome = Forget(call.Sender, target);
        if (outcome.Ok)
            call.Reply(outcome.Message);
        else
            call.Refuse(outcome.Message);
    }

    private PlayerAsk? Find(OnlinePlayer asker, OnlinePlayer asked) => _offers.FirstOrDefault(offer => offer.Asker == asker && offer.Asked == asked);

    // Both now know each other, so the two read each other's names in the line and those around read them as they know them.
    private LoreOutcome Accept(PlayerAsk ask)
    {
        Withdraw(ask);
        introductions.Introduce(ask.Asker, ask.Asked);
        chat.SendAround(ask.Asker, config.Chat.Say, ChatLineKind.Action, LoreText.IntroDone(new ShownName(ask.Asker), new ShownName(ask.Asked)));
        chat.Notify(ask.Asker, LoreText.IntroKnowNow(new ShownName(ask.Asked)));
        chat.Notify(ask.Asked, LoreText.IntroKnowNow(new ShownName(ask.Asker)));
        return new LoreOutcome(true, LoreText.IntroKnowNow(new ShownName(ask.Asker)));
    }

    private void Decline(PlayerAsk ask)
    {
        if (!Withdraw(ask))
            return;

        chat.Notify(ask.Asked, LoreText.IntroYouDeclined(new ShownName(ask.Asker)));
        if (ask.Asker.IsLive)
            chat.Notify(ask.Asker, LoreText.IntroDeclined(new ShownName(ask.Asked)));
    }

    private void Expire(PlayerAsk ask)
    {
        if (Withdraw(ask) && ask.Asker.IsLive)
            chat.Notify(ask.Asker, LoreText.IntroTimeout(new ShownName(ask.Asked)));
    }

    // False when it was not waiting any more.
    private bool Withdraw(PlayerAsk ask)
    {
        asks.Close(ask);
        ask.Timeout?.Cancel();
        return _offers.Remove(ask);
    }

    private void OnLeft(OnlinePlayer player)
    {
        foreach (var ask in _offers.Where(offer => offer.Asker == player || offer.Asked == player).ToList())
        {
            Withdraw(ask);
            if (ask.Asked == player && ask.Asker.IsLive)
                chat.Notify(ask.Asker, LoreText.IntroTimeout(new ShownName(player)));
        }

        _pacing.Forget(player);
    }

    private void Tell(OnlinePlayer player, LoreOutcome outcome)
    {
        if (outcome.Ok)
            chat.Reply(player, outcome.Message);
        else
            chat.Refuse(player, outcome.Message);
    }
}
