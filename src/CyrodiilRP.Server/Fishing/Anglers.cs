using System.Numerics;
using System.Text.Json;
using CyrodiilRP.Common.Chat;
using CyrodiilRP.Common.Fishing;
using CyrodiilRP.Server.Admin;
using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Interactions;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Network;
using CyrodiilRP.Server.Players;
using CyrodiilRP.Server.Sanctions;
using Microsoft.Extensions.Logging;
using OblivionMP.SDK.Common.Archetypes;
using OblivionMP.SDK.Common.Archetypes.Mixins;
using ReadyM.Api.Idents;
using ReadyM.SDK.Archetypes.Core;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Server.Entities;

namespace CyrodiilRP.Server.Fishing;

// Fishing from the G wheel. The player's game finds the water and where the float lands; the server holds the character
// still, picks what bites at each cast, keeps every time and the whole fight on its own clock (FishingLine), decides
// every catch and gives it or lets it go again, wears the rod, and tells the games near the angler what their rod, float
// and fish do.
[Service]
internal sealed partial class Anglers(
    SelfMenu self,
    RpcHandlers rpc,
    OnlinePlayers online,
    Proximity proximity,
    IEntities entities,
    Freezes freezes,
    FishItems items,
    FishingStock stock,
    FishingSkills skills,
    FishingSurroundings surroundings,
    RpChat chat,
    RpConfig config,
    Timers timers,
    SystemClock clock,
    ILogger logger)
{
    private const string FishOption = "self.fish";
    private const string StopOption = "self.fish_stop";
    private const int FishOrder = 35;
    private const string TickJob = "fishing.tick";
    private const int LeanLeft = -1;
    private const int LeanRight = 1;
    private const float QuarterTurn = 90f;
    private const float HalfTurn = 180f;
    private const int Decimals = 3;

    private readonly AnglerRegistry _anglers = new();
    private readonly FishingLookRegistry _looks = new();
    private readonly Random _random = new();
    private long _seersSeenAtMs;

    private void Start()
    {
        self.Add(new SelfOption { Id = FishOption, Label = FishingText.OptionFish, Icon = WheelIcons.Fish, Order = FishOrder, State = CheckFish, Run = Look });
        self.Add(new SelfOption
        {
            Id = StopOption,
            Label = FishingText.OptionStop,
            Icon = WheelIcons.Stop,
            Order = FishOrder,
            State = player => OptionState.OpenIf(_anglers.Contains(player)),
            Run = player => End(player, notice: null),
        });
        rpc.FishingSpotReported += OnSpot;
        rpc.FishingCastMade += OnCast;
        rpc.FishingSpacePressed += OnSpace;
        rpc.FishingLeaned += OnLean;
        rpc.FishingStopAsked += OnStopAsked;
        rpc.FishingReleaseAsked += OnReleaseAsked;
        rpc.FishingTwitched += OnTwitched;
        online.Left += OnLeft;
        freezes.Froze += OnFroze;
        skills.BaitChosen += OnBaitChosen;
        timers.RunEvery(TickJob, FishingRules.Tick, Tick);
    }

    private OptionState CheckFish(OnlinePlayer player)
    {
        if (_anglers.Contains(player) || !player.IsPlaying)
            return OptionState.Hidden;

        return OptionState.OpenUnless(FindRefusal(player));
    }

    // Null when the player may fish now.
    private Phrase? FindRefusal(OnlinePlayer player)
    {
        if (entities.TryLookup(player.Id, out Player entity) && entity.IsFrozen)
            return FishingText.Held;

        if (skills.Find(player) is null)
            return FishingText.NotReady;

        if (stock.FindBestRod(player) is not null)
            return null;

        return stock.ListRods(player).Count > 0 ? FishingText.RodBroken : FishingText.NoRod;
    }

    private void Look(OnlinePlayer player)
    {
        if (stock.FindBestRod(player) is not { } rod)
        {
            chat.Notify(player, FishingText.NoRod);
            return;
        }

        _looks.Ask(player, clock.MonotonicMs);
        rpc.SendFishingLook(player.Id, rod.Rod.ReachCm);
    }

    private void OnSpot(PlayerId sender, FishingSpotReport report)
    {
        if (!online.TryGet(sender, out var player))
            return;

        if (!_looks.TryTake(player, clock.MonotonicMs))
        {
            logger.LogWarning("{Player} told where the water is without being asked, or too late.", player.Ref);
            return;
        }

        if (!Enum.TryParse<FishingVerdict>(report.Verdict, out var verdict) || !Enum.IsDefined(verdict)
            || !float.IsFinite(report.Yaw) || !float.IsFinite(report.WaterZ))
        {
            logger.LogWarning("{Player} told where the water is in no way the mode knows: {Verdict}, {Yaw}, {WaterZ}.",
                player.Ref, report.Verdict, report.Yaw, report.WaterZ);
            return;
        }

        Phrase? refusal = verdict switch
        {
            FishingVerdict.Found => null,
            FishingVerdict.NoWater => FishingText.NoWater,
            FishingVerdict.Swimming => FishingText.Swimming,
            FishingVerdict.NotInWorld => FishingText.NotInWorld,
        };
        if (refusal is not null)
            chat.Notify(player, refusal);
        else
            Begin(player, report.Yaw, report.WaterZ);
    }

    private void Begin(OnlinePlayer player, float yaw, float waterZ)
    {
        if (_anglers.Contains(player))
            return;

        if (!entities.TryLookup(player.Id, out Player entity) || !entities.TryLookup(player.Id, out MainCharacter main)
            || !proximity.TryLocate(player.Id, out var area, out var stand))
        {
            chat.Notify(player, FishingText.NotInWorld);
            return;
        }

        if (FindRefusal(player) is { } refusal || stock.FindBestRod(player) is not { } rod)
        {
            chat.Notify(player, FindRefusal(player) ?? FishingText.NoRod);
            return;
        }

        if (!float.IsFinite(waterZ) || Math.Abs(stand.Z - waterZ) > FishingRules.WaterBelowMost)
        {
            logger.LogWarning("{Player} told of water at {WaterZ} while standing at {Stand}.", player.Ref, waterZ, stand);
            chat.Notify(player, FishingText.NoWater);
            return;
        }

        entity.IsFrozen = true;
        main.Rotation = yaw;
        var angler = new Angler(player, area, stand, yaw, waterZ, new FishingLine(_random.NextDouble), rod, BaitKind.None, CountBait(player),
            Quarry: null, Float: null, FloatShown: null, FishShown: false, DipEndsAtMs: 0, NextThrashAtMs: 0, Nibbles: 0, Bites: 0, Outcome: null,
            Held: null, Twitches: 0, TwitchResult.None, Bend: 0, Seers: new HashSet<PlayerId>());
        rpc.SendFishingAim(player.Id, yaw, waterZ, rod.Rod.ReachCm);
        angler = SeeAround(angler);
        _anglers.Set(angler);
        SendPage(angler);
    }

    private void OnCast(PlayerId sender, Vector3 point, float nearest)
    {
        if (!online.TryGet(sender, out var player) || !_anglers.TryGet(player, out var angler) || angler.Line.Stage != FishingStage.Aim)
            return;

        if (stock.FindBestRod(player) is not { } rod)
        {
            End(player, FishingText.RodGone);
            return;
        }

        var reach = Vector2.Distance(new Vector2(point.X, point.Y), new Vector2(angler.Stand.X, angler.Stand.Y));
        var aim = MathF.Atan2(point.Y - angler.Stand.Y, point.X - angler.Stand.X) * FishingRules.DegreesPerRadian;
        if (!float.IsFinite(point.X) || !float.IsFinite(point.Y) || !float.IsFinite(point.Z) || !float.IsFinite(nearest)
            || reach > rod.Rod.ReachCm + FishingReach.Slack || reach < FishingReach.Nearest - FishingReach.Slack
            || Math.Abs(FishingShot.Turn(aim - angler.Yaw)) > FishingReach.AimDegrees + FishingRules.AimSlackDegrees
            || Math.Abs(point.Z - angler.WaterZ) > FishingReach.SameWater)
        {
            logger.LogWarning("{Player} cast to {Point}, {Reach} cm away, out of the rod's reach or off the water at {WaterZ}.",
                player.Ref, point, reach, angler.WaterZ);
            rpc.SendFishingAim(player.Id, angler.Yaw, angler.WaterZ, rod.Rod.ReachCm);
            return;
        }

        if (!entities.TryLookup(player.Id, out MainCharacter main) || !entities.TryGetScope(main, out Area area))
        {
            End(player, FishingText.NotInWorld);
            return;
        }

        var progress = skills.Find(player) ?? throw new InvalidOperationException($"{player.Ref} fishes before their fishing is read.");
        var level = progress.Skill.Level;
        var conditions = surroundings.Read(area, point);
        var bait = PutOnBait(player, progress.Bait);
        var quarry = CatchPicker.Pick(conditions, bait, level, _random.NextDouble);
        var cast = new LineCast(quarry.Fight, quarry.SizeShare, CatchPicker.WaitFactor(level, rod.Rod, bait),
            FishingRules.StrikeWindow + FishingSkillRules.StrikeBonus(level), rod.Line * FishingSkillRules.LineTolerance(level), rod.Rod.ReachCm,
            reach, Math.Clamp(nearest, FishingReach.Nearest, Math.Max(FishingReach.Nearest, reach)), FishingRules.TwitchOf(bait));
        var now = clock.MonotonicMs;
        angler.Line.Cast(now, cast);
        angler = Wield(angler, rod) with
        {
            Float = point, BaitUsed = bait, BaitCount = CountBait(player), Quarry = quarry, Twitches = 0, Twitch = TwitchResult.None,
        };
        foreach (var seer in angler.Seers)
            rpc.SendAnglerCast(seer, player.Id.RawValue, point.X, point.Y, point.Z);

        angler = ShowFloat(angler, now);
        _anglers.Set(angler);
        SendPage(angler);
    }

    // The bait chosen, one piece of it taken unless it stays on the line, or a bare hook when the pack has none left.
    private BaitKind PutOnBait(OnlinePlayer player, BaitKind chosen)
    {
        if (chosen == BaitKind.None)
            return BaitKind.None;

        var bait = BaitCatalog.Find(chosen);
        var taken = stock.TryFind(bait, out var item) && bait.Use switch
        {
            BaitUse.PerCast => stock.TryTake(player, item, 1),
            BaitUse.LostWithLine => stock.Count(player, item) > 0,
        };
        if (taken)
            return chosen;

        chat.Notify(player, FishingText.BaitOut(bait.Name));
        return BaitKind.None;
    }

    private int CountBait(OnlinePlayer player)
    {
        if (skills.Find(player) is not { Bait: not BaitKind.None } progress)
            return 0;

        return stock.TryFind(BaitCatalog.Find(progress.Bait), out var item) ? stock.Count(player, item) : 0;
    }

    private void OnSpace(PlayerId sender, bool down, int reactionMs)
    {
        if (!online.TryGet(sender, out var player) || !_anglers.TryGet(player, out var angler))
            return;

        var now = clock.MonotonicMs;
        var change = down ? angler.Line.Press(now, reactionMs) : angler.Line.Release(now);
        Apply(angler, change, now);
    }

    private void OnLean(PlayerId sender, int lean)
    {
        if (!online.TryGet(sender, out var player) || !_anglers.TryGet(player, out var angler))
            return;

        if (lean is < LeanLeft or > LeanRight)
        {
            logger.LogWarning("{Player} leaned the rod {Lean}, which is no way a rod leans.", player.Ref, lean);
            return;
        }

        var now = clock.MonotonicMs;
        Apply(angler, angler.Line.LeanTo(now, lean), now);
    }

    private void OnStopAsked(PlayerId sender)
    {
        if (online.TryGet(sender, out var player))
            End(player, notice: null);
    }

    private void OnTwitched(PlayerId sender)
    {
        if (!online.TryGet(sender, out var player) || !_anglers.TryGet(player, out var angler))
            return;

        var now = clock.MonotonicMs;
        Apply(angler, angler.Line.Twitch(now), now);
    }

    // The float hops in along the line to where the twitch pulled it, with a plop. A fish that bolted swims off.
    private Angler Twitched(Angler angler, TwitchResult result)
    {
        if (angler.Float is not { } rest)
            throw new InvalidOperationException($"{angler.Player.Ref} twitched a line that has no float.");

        var outward = new Vector2(rest.X - angler.Stand.X, rest.Y - angler.Stand.Y);
        var along = outward.LengthSquared() > 0 ? Vector2.Normalize(outward) : Vector2.Zero;
        var distance = (float)angler.Line.FloatDistanceCm;
        var pulled = rest with { X = angler.Stand.X + along.X * distance, Y = angler.Stand.Y + along.Y * distance };
        Splash(angler, FishingSplashKind.Twitch, pulled);
        var twitched = angler with { Float = pulled, Twitches = angler.Twitches + 1, Twitch = result };
        if (result != TwitchResult.Spooked || !angler.FishShown)
            return twitched;

        // Every game takes the fish away itself once it swam off: none is shown until the next comes.
        return ShowFish(twitched, FishMove.Escaped, rest with { Z = angler.WaterZ }, angler.Yaw) with { FishShown = false };
    }

    // The fish held up goes back into the water for more of its xp: out of the pack, seen and read by those near.
    private void OnReleaseAsked(PlayerId sender)
    {
        if (!online.TryGet(sender, out var player) || !_anglers.TryGet(player, out var angler) || angler.Line.Stage != FishingStage.Rest
            || angler.Held is not { } held || angler.Outcome is not { } outcome)
            return;

        // Eaten or dropped since it was landed: there is nothing left to let go.
        if (!stock.TryFind(held.Fish.Item.Key, out var item) || !stock.TryTake(player, item, 1))
        {
            angler = angler with { Held = null, Outcome = outcome with { ReleaseXp = 0 } };
            _anglers.Set(angler);
            SendPage(angler);
            return;
        }

        var gain = skills.RecordRelease(player, held.ReleaseXp);
        angler.Line.EndShow(clock.MonotonicMs);
        Splash(angler, FishingSplashKind.Release, held.WaterAt);
        foreach (var seer in angler.Seers)
            rpc.SendFishingFish(seer, player.Id.RawValue, nameof(FishMove.Escaped), held.WaterAt.X, held.WaterAt.Y, held.WaterAt.Z, angler.Yaw,
                held.Fish.LengthCm);

        chat.SendAround(player, config.Chat.Emote, ChatLineKind.Action,
            FishingText.Released(new ShownName(player), FishNames.Landed(held.Fish.Fish.Species)));
        angler = angler with
        {
            FishShown = true,
            Held = null,
            Outcome = outcome with { Kind = FishingOutcome.Released, Xp = outcome.Xp + gain.Xp, LevelUp = gain.LevelUp ?? outcome.LevelUp, ReleaseXp = 0 },
        };
        _anglers.Set(angler);
        SendPage(angler);
    }

    private void OnFroze(OnlinePlayer player) => End(player, notice: null);

    private void OnBaitChosen(OnlinePlayer player)
    {
        if (!_anglers.TryGet(player, out var angler))
            return;

        angler = angler with { BaitCount = CountBait(player) };
        _anglers.Set(angler);
        SendPage(angler);
    }

    private void OnLeft(OnlinePlayer player)
    {
        _looks.Forget(player);
        if (_anglers.Remove(player, out var angler))
            Hide(angler);
    }

    private void Tick()
    {
        var now = clock.MonotonicMs;
        foreach (var player in _looks.TakeUnanswered(now))
            chat.Notify(player, FishingText.NoAnswer);

        var seeAround = now - _seersSeenAtMs >= (long)FishingRules.SeersEvery.TotalMilliseconds;
        if (seeAround)
            _seersSeenAtMs = now;

        foreach (var angler in _anglers.ListAll())
        {
            if (!proximity.TryLocate(angler.Player.Id, out var area, out var position) || area != angler.Area
                || Vector3.Distance(position, angler.Stand) > FishingRules.StayWithin)
            {
                End(angler.Player, FishingText.Moved);
                continue;
            }

            Apply(seeAround ? SeeAround(angler) : angler, angler.Line.Tick(now), now);
        }
    }

    private void Apply(Angler angler, LineChange change, long now)
    {
        var next = change switch
        {
            LineChange.None or LineChange.Cast or LineChange.Fought => angler,
            LineChange.Approach => ShowFish(angler, FishMove.Approach),
            LineChange.Nibble => Nibbled(angler, now),
            LineChange.Drew => Twitched(angler, TwitchResult.Drew),
            LineChange.Troubled => Twitched(angler, TwitchResult.Troubled),
            LineChange.Provoked => Twitched(angler, TwitchResult.Provoked),
            LineChange.Spooked => Twitched(angler, TwitchResult.Spooked),
            LineChange.Bite => Bitten(angler),
            LineChange.Hooked => ShowFish(angler, FishMove.Hooked) with { NextThrashAtMs = now },
            LineChange.Turned => angler with { NextThrashAtMs = now },
            LineChange.Jumped => Jumped(angler, now),
            LineChange.Caught => Land(angler),
            LineChange.LostEarly => Lose(angler, FishingOutcome.LostEarly),
            LineChange.LostNibble => Lose(angler, FishingOutcome.LostNibble),
            LineChange.LostLate => Lose(angler, FishingOutcome.LostLate),
            LineChange.LostSnapped => Lose(angler, FishingOutcome.LostSnapped),
            LineChange.LostSlack => Lose(angler, FishingOutcome.LostSlack),
            LineChange.LostRunaway => Lose(angler, FishingOutcome.LostRunaway),
            LineChange.Ready => Ready(angler),
        };
        if (next is null)
            return;

        next = Thrash(next, now);
        next = ShowFloat(next, now);
        next = ShowBend(next);
        _anglers.Set(next);
        if (change != LineChange.None)
            SendPage(next);
    }

    // Null when the fishing ended: no whole rod is left.
    private Angler? Ready(Angler angler)
    {
        var player = angler.Player;
        if (stock.FindBestRod(player) is not { } rod)
        {
            _anglers.Set(angler);
            End(player, FishingText.NoRodLeft);
            return null;
        }

        angler = Wield(TakeFishAway(angler), rod) with { Held = null };
        rpc.SendFishingAim(player.Id, angler.Yaw, angler.WaterZ, rod.Rod.ReachCm);
        return angler;
    }

    // The rod in hand, shown again to everyone near when it is another grade.
    private Angler Wield(Angler angler, RodItem rod)
    {
        if (rod.Rod.Tier != angler.Rod.Rod.Tier)
        {
            foreach (var seer in angler.Seers)
                rpc.SendAnglerShown(seer, angler.Player.Id.RawValue, true, rod.Rod.Tier.ToString());
        }

        return angler with { Rod = rod };
    }

    private Angler Nibbled(Angler angler, long now)
    {
        if (angler.Float is { } at)
            Splash(angler, FishingSplashKind.Nibble, at);

        return ShowFish(angler, FishMove.Nibble) with { Nibbles = angler.Nibbles + 1, DipEndsAtMs = now + (long)FishingRules.NibbleDips.TotalMilliseconds };
    }

    private Angler Bitten(Angler angler)
    {
        rpc.SendFishingBite(angler.Player.Id);
        if (angler.Float is { } at)
            Splash(angler, FishingSplashKind.Bite, at);

        return ShowFish(angler, FishMove.Bite) with { Bites = angler.Bites + 1 };
    }

    private Angler Jumped(Angler angler, long now)
    {
        if (FindFloat(angler, now) is { } under)
        {
            var at = under with { Z = angler.WaterZ };
            Splash(angler, FishingSplashKind.Jump, at);
            angler = ShowFish(angler, FishMove.Jump, at, HeadingOf(angler));
        }

        return angler with { NextThrashAtMs = now + (long)FishingRules.ThrashSoonest.TotalMilliseconds };
    }

    // The water splashes at the float while the fish runs, every so often.
    private Angler Thrash(Angler angler, long now)
    {
        if (angler.Line.Stage != FishingStage.Fight || angler.Line.Pull == FishPull.Rest || now < angler.NextThrashAtMs
            || FindFloat(angler, now) is not { } under)
            return angler;

        Splash(angler, FishingSplashKind.Thrash, under with { Z = angler.WaterZ });
        return angler with { NextThrashAtMs = now + (long)FishingRules.ThrashWait(_random.NextDouble()).TotalMilliseconds };
    }

    // A find in some of the landed bites, a fish in the rest, into the pack; the rod may wear. A fish in the pack may be
    // let go while it is held up.
    private Angler Land(Angler angler)
    {
        var player = angler.Player;
        var number = (angler.Outcome?.Number ?? 0) + 1;
        var at = angler.FloatShown ?? angler.Float ?? angler.Stand;
        Splash(angler, FishingSplashKind.Catch, at with { Z = angler.WaterZ });
        var worn = Wear(angler, snapped: false);
        angler = angler with { Rod = worn ?? angler.Rod };
        FishingOutcomeView outcome;
        HeldCatch? held = null;
        bool given;
        switch (angler.Quarry)
        {
            case FishQuarry fish:
            {
                var item = fish.Item;
                given = items.TryGive(player, item);
                var gain = skills.RecordCatch(player, fish);
                angler = ShowFish(angler, FishMove.Landed, angler.Stand, angler.Yaw);
                var line = item.Trophy
                    ? FishingText.LandedTrophy(new ShownName(player), FishNames.Landed(fish.Fish.Species), fish.LengthCm)
                    : FishingText.Landed(new ShownName(player), FishNames.Landed(fish.Fish.Species), fish.LengthCm);
                chat.SendAround(player, config.Chat.Emote, ChatLineKind.Action, line);
                if (given)
                    held = new HeldCatch(fish, at with { Z = angler.WaterZ }, FishingSkillRules.ReleaseXp(gain.Xp));
                outcome = new FishingOutcomeView(number, FishingOutcome.Caught, fish.Fish.Species, item.Name, fish.LengthCm, fish.Grams, item.Trophy,
                    gain.Record, gain.Xp, gain.LevelUp, fish.Fish.Rare, worn?.Condition, BaitLost: false, held?.ReleaseXp ?? 0);
                break;
            }
            case FindQuarry find:
            {
                var name = ItemCatalog.Shipped.FindByForm(find.Find.Item.Value)?.Name
                           ?? throw new InvalidOperationException($"The find {find.Find.Kind} is no item of the game's the mode knows.");
                given = items.TryGive(player, find.Find);
                var gain = skills.RecordFind(player);
                outcome = new FishingOutcomeView(number, FishingOutcome.Found, Species: null, name, LengthCm: 0, Grams: 0, Trophy: false,
                    Record: false, gain.Xp, gain.LevelUp, Rare: false, worn?.Condition, BaitLost: false, ReleaseXp: 0);
                break;
            }
            default:
                throw new InvalidOperationException($"{player.Ref} landed a line nothing was on.");
        }

        if (!given)
        {
            logger.LogError("{Player} landed {Catch}, which could not be put in their pack.", player.Ref, outcome.Name);
            chat.Notify(player, FishingText.NotGiven);
        }

        return angler with { Float = null, Quarry = null, BaitCount = CountBait(player), Outcome = outcome, Held = held };
    }

    // A fish that got away swims off. One that was hooked may wear the rod and take the lure along.
    private Angler Lose(Angler angler, FishingOutcome why)
    {
        var hooked = why switch
        {
            FishingOutcome.LostSnapped or FishingOutcome.LostSlack or FishingOutcome.LostRunaway => true,
            FishingOutcome.LostEarly or FishingOutcome.LostNibble or FishingOutcome.LostLate => false,
            FishingOutcome.Caught or FishingOutcome.Found or FishingOutcome.Released =>
                throw new ArgumentOutOfRangeException(nameof(why), why, "A catch is no loss."),
        };
        var at = angler.FloatShown ?? angler.Float;
        if (why == FishingOutcome.LostSnapped && at is { } snapped)
            Splash(angler, FishingSplashKind.Snap, snapped with { Z = angler.WaterZ });
        if (angler.FishShown && at is { } escapedAt)
            angler = ShowFish(angler, FishMove.Escaped, escapedAt with { Z = angler.WaterZ }, HeadingOf(angler));

        var worn = hooked ? Wear(angler, snapped: why == FishingOutcome.LostSnapped) : null;
        var baitLost = (why is FishingOutcome.LostSnapped or FishingOutcome.LostRunaway) && angler.BaitUsed == BaitKind.Lure && TakeLure(angler.Player);
        var fish = hooked ? angler.Quarry as FishQuarry : null;
        var outcome = new FishingOutcomeView((angler.Outcome?.Number ?? 0) + 1, why, fish?.Fish.Species, fish?.Item.Name, LengthCm: 0, Grams: 0,
            Trophy: false, Record: false, Xp: 0, LevelUp: null, Rare: false, worn?.Condition, baitLost, ReleaseXp: 0);
        return angler with { Rod = worn ?? angler.Rod, Float = null, Quarry = null, BaitCount = CountBait(angler.Player), Outcome = outcome };
    }

    private bool TakeLure(OnlinePlayer player)
    {
        var lure = BaitCatalog.Find(BaitKind.Lure);
        if (stock.TryFind(lure, out var item) && stock.TryTake(player, item, 1))
            return true;

        logger.LogWarning("{Player} lost a lure with the line, but no lure was left in their pack to take.", player.Ref);
        return false;
    }

    // One step with a chance of one in the rod's WearOneIn, likelier on a snap: the rod in the pack becomes the next
    // condition's item, which comes back. Null when it did not wear.
    private RodItem? Wear(Angler angler, bool snapped)
    {
        var rod = angler.Rod;
        var chance = (snapped ? FishingRules.SnapWears : 1) / (double)rod.Rod.WearOneIn;
        if (_random.NextDouble() >= chance || rod.WornDown is not { } worn)
            return null;

        var player = angler.Player;
        if (!stock.TryExchange(player, rod.Key, worn.Key))
        {
            logger.LogWarning("{Player}'s {Rod} wore down, but it was no longer in their pack.", player.Ref, rod.Key);
            return null;
        }

        chat.Notify(player, worn.Usable ? FishingText.RodWore(worn.Name) : FishingText.RodBroke(worn.Name));
        return worn;
    }

    private Angler ShowFish(Angler angler, FishMove move) =>
        angler.Float is { } at ? ShowFish(angler, move, at with { Z = angler.WaterZ }, angler.Yaw + HalfTurn) : angler;

    // Only a fish is shown: a find comes up without one.
    private Angler ShowFish(Angler angler, FishMove move, Vector3 at, float yaw)
    {
        if (angler.Quarry is not FishQuarry fish)
            return angler;

        var name = move.ToString();
        foreach (var seer in angler.Seers)
            rpc.SendFishingFish(seer, angler.Player.Id.RawValue, name, at.X, at.Y, at.Z, yaw, fish.LengthCm);

        return angler with { FishShown = true };
    }

    // The fish drawn so far, in the water or in the hand, is taken away.
    private Angler TakeFishAway(Angler angler)
    {
        if (!angler.FishShown)
            return angler;

        foreach (var seer in angler.Seers)
            rpc.SendFishingFish(seer, angler.Player.Id.RawValue, nameof(FishMove.Gone), angler.Stand.X, angler.Stand.Y, angler.Stand.Z, angler.Yaw, 0);

        return angler with { FishShown = false };
    }

    // The way a fish heads as it runs, leaps or gets away: to the angler's side it runs to, or away.
    private static float HeadingOf(Angler angler) => angler.Line.Pull switch
    {
        FishPull.Left => angler.Yaw - QuarterTurn,
        FishPull.Right => angler.Yaw + QuarterTurn,
        FishPull.Away or FishPull.Rest => angler.Yaw,
    };

    // Where the games near should draw the float now, told when it moved, under the water only when it moved far enough.
    private Angler ShowFloat(Angler angler, long now)
    {
        var at = FindFloat(angler, now);
        if (at == angler.FloatShown)
            return angler;

        if (at is { } point && angler.FloatShown is { } shown && angler.Line.Stage == FishingStage.Fight
            && Vector3.Distance(point, shown) < FishingRules.FloatMovesAtLeast)
            return angler;

        foreach (var seer in angler.Seers)
        {
            if (at is { } moved)
                rpc.SendFishingFloat(seer, angler.Player.Id.RawValue, moved.X, moved.Y, moved.Z);
            else
                rpc.SendFishingFloatGone(seer, angler.Player.Id.RawValue);
        }

        return angler with { FloatShown = at };
    }

    // The games near are told how far the rod bends when that changes: by the line's tension in a fight, a little at
    // the bite, straight otherwise.
    private Angler ShowBend(Angler angler)
    {
        var bend = angler.Line.Stage switch
        {
            FishingStage.Fight => FishFightRules.BendOf(angler.Line.Tension, angler.Bend),
            FishingStage.Bite => 1,
            FishingStage.Aim or FishingStage.Waiting or FishingStage.Rest => 0,
        };
        if (bend == angler.Bend)
            return angler;

        foreach (var seer in angler.Seers)
            rpc.SendAnglerRod(seer, angler.Player.Id.RawValue, bend);

        return angler with { Bend = bend };
    }

    // Riding on the water, dipped by a nibble, pulled under by the bite, and dragged about under the water by the fish:
    // out or in along the line and aside with its runs.
    private static Vector3? FindFloat(Angler angler, long now)
    {
        if (angler.Float is not { } rest)
            return null;

        return angler.Line.Stage switch
        {
            FishingStage.Waiting => rest with { Z = now < angler.DipEndsAtMs ? rest.Z - FishingRules.NibbleDip : rest.Z + FishingRules.FloatRides },
            FishingStage.Bite => rest with { Z = rest.Z - FishingRules.FloatUnder },
            FishingStage.Fight => Drag(angler, rest),
            FishingStage.Aim or FishingStage.Rest => null,
        };
    }

    private static Vector3 Drag(Angler angler, Vector3 rest)
    {
        var outward = new Vector2(rest.X - angler.Stand.X, rest.Y - angler.Stand.Y);
        var facing = FishingShot.Forward(angler.Yaw);
        var along = outward.LengthSquared() > 0 ? Vector2.Normalize(outward) : new Vector2(facing.X, facing.Y);
        var aside = FishingShot.Right(angler.Yaw);
        var line = angler.Line;
        return new Vector3(
            angler.Stand.X + along.X * (float)line.DistanceCm + aside.X * (float)line.DriftCm,
            angler.Stand.Y + along.Y * (float)line.DistanceCm + aside.Y * (float)line.DriftCm,
            angler.WaterZ - FishingRules.FloatUnder);
    }

    private void Splash(Angler angler, FishingSplashKind kind, Vector3 at)
    {
        var name = kind.ToString();
        foreach (var seer in angler.Seers)
            rpc.SendFishingSplash(seer, name, at.X, at.Y, at.Z);
    }

    // Who is near now: a newcomer is told of the rod, the float and a fish by it, one who went away that they are gone.
    private Angler SeeAround(Angler angler)
    {
        var near = proximity.FindAround(angler.Player.Id, FishingRules.SeenWithin).ToHashSet();
        var id = angler.Player.Id.RawValue;
        var fish = NewcomerFish(angler);
        foreach (var seer in near.Except(angler.Seers))
        {
            rpc.SendAnglerShown(seer, id, true, angler.Rod.Rod.Tier.ToString());
            if (angler.Bend > 0)
                rpc.SendAnglerRod(seer, id, angler.Bend);
            if (angler.FloatShown is { } shown)
                rpc.SendFishingFloat(seer, id, shown.X, shown.Y, shown.Z);
            if (fish is { } move && angler.Quarry is FishQuarry quarry && angler.Float is { } at)
                rpc.SendFishingFish(seer, id, move.ToString(), at.X, at.Y, angler.WaterZ, angler.Yaw + HalfTurn, quarry.LengthCm);
        }

        foreach (var gone in angler.Seers.Except(near))
        {
            if (angler.FishShown)
                rpc.SendFishingFish(gone, id, nameof(FishMove.Gone), angler.Stand.X, angler.Stand.Y, angler.Stand.Z, angler.Yaw, 0);
            rpc.SendFishingFloatGone(gone, id);
            rpc.SendAnglerShown(gone, id, false, string.Empty);
        }

        return angler with { Seers = near };
    }

    // What a game that comes near should draw of a fish in the water: one circling the float, or one on the line.
    private static FishMove? NewcomerFish(Angler angler)
    {
        if (!angler.FishShown || angler.Float is null)
            return null;

        return angler.Line.Stage switch
        {
            FishingStage.Waiting or FishingStage.Bite => FishMove.Approach,
            FishingStage.Fight => FishMove.Hooked,
            FishingStage.Aim or FishingStage.Rest => null,
        };
    }

    // Stopped from the wheel or the keys, moved off, held by the staff: the character is free again, unless the staff
    // hold it or it flies.
    private void End(OnlinePlayer player, Phrase? notice)
    {
        _looks.Forget(player);
        if (!_anglers.Remove(player, out var angler))
            return;

        // Walking away from a fish on the line cuts it, at what a snap would cost.
        if (angler.Line.Stage == FishingStage.Fight)
            angler = Lose(angler, FishingOutcome.LostSnapped);

        if (entities.TryLookup(player.Id, out Player entity) && !entity.IgnoresCollision)
            entity.IsFrozen = freezes.IsFrozen(player);

        Hide(angler);
        rpc.SendFishingEnded(player.Id);
        if (notice is not null)
            chat.Notify(player, notice);
    }

    private void Hide(Angler angler)
    {
        var id = angler.Player.Id.RawValue;
        foreach (var seer in angler.Seers.Where(seer => seer != angler.Player.Id || angler.Player.IsLive))
        {
            if (angler.FishShown)
                rpc.SendFishingFish(seer, id, nameof(FishMove.Gone), angler.Stand.X, angler.Stand.Y, angler.Stand.Z, angler.Yaw, 0);
            rpc.SendFishingFloatGone(seer, id);
            rpc.SendAnglerShown(seer, id, false, string.Empty);
        }
    }

    private void SendPage(Angler angler)
    {
        var line = angler.Line;
        var progress = skills.Find(angler.Player) ?? throw new InvalidOperationException($"{angler.Player.Ref} fishes before their fishing is read.");
        var fight = line.Stage == FishingStage.Fight
            ? new FishingFightView(Math.Round(line.Tension, Decimals), FishFightRules.TensionWarn, Math.Round(line.Stamina, Decimals),
                (int)Math.Round(line.DistanceCm), line.Pull, line.Lean, line.Reeling, Math.Round(line.SlackShare, Decimals), line.Feel)
            : null;
        var twitch = line.Stage == FishingStage.Waiting
            ? new FishingTwitchView(angler.Twitches, angler.Twitch, (int)line.CalmIn(clock.MonotonicMs).TotalMilliseconds, line.IsPulledIn)
            : null;
        var rod = angler.Rod;
        var bait = progress.Bait == BaitKind.None ? null : new FishingBaitView(progress.Bait, BaitCatalog.Find(progress.Bait).Name, angler.BaitCount);
        var skill = new FishingSkillView(progress.Skill.Level, progress.Skill.Xp, progress.Skill.XpNext);
        var view = new FishingView(line.Stage, angler.Nibbles, angler.Bites, (int)line.StrikeWindow.TotalMilliseconds, fight, twitch,
            new FishingRodView(rod.Name, rod.Rod.Tier, rod.Condition, (int)rod.Rod.ReachCm), bait, skill, angler.Outcome);
        rpc.SendFishingPage(angler.Player.Id, JsonSerializer.Serialize(view, FishingJson.Default.FishingView));
    }
}
