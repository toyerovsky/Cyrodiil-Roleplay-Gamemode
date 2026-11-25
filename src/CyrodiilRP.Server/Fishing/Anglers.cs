using System.Numerics;
using System.Text.Json;
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
// still, keeps every time of the line on its own clock (FishingLine), decides every catch and gives it, and tells the
// games near the angler where the float lies and where the water splashes.
[Service]
internal sealed partial class Anglers(
    SelfMenu self,
    RpcHandlers rpc,
    OnlinePlayers online,
    Proximity proximity,
    IEntities entities,
    Freezes freezes,
    FishItems items,
    RpChat chat,
    Timers timers,
    SystemClock clock,
    ILogger logger)
{
    private const string FishOption = "self.fish";
    private const string StopOption = "self.fish_stop";
    private const int FishOrder = 35;
    private const string TickJob = "fishing.tick";

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
        rpc.FishingStopAsked += OnStopAsked;
        online.Left += OnLeft;
        freezes.Froze += OnFroze;
        timers.RunEvery(TickJob, FishingRules.Tick, Tick);
    }

    private OptionState CheckFish(OnlinePlayer player)
    {
        if (_anglers.Contains(player) || !player.IsPlaying)
            return OptionState.Hidden;

        return entities.TryLookup(player.Id, out Player entity) && entity.IsFrozen ? OptionState.Close(FishingText.Held) : OptionState.Open;
    }

    private void Look(OnlinePlayer player)
    {
        _looks.Ask(player, clock.MonotonicMs);
        rpc.SendFishingLook(player.Id);
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

        if (entity.IsFrozen)
        {
            chat.Notify(player, FishingText.Held);
            return;
        }

        entity.IsFrozen = true;
        main.Rotation = yaw;
        var angler = new Angler(player, area, stand, yaw, waterZ, new FishingLine(_random.NextDouble), Bait: null, FloatShown: null,
            DipEndsAtMs: 0, NextThrashAtMs: 0, Nibbles: 0, Bites: 0, Outcome: null, Seers: new HashSet<PlayerId>());
        rpc.SendFishingAim(player.Id, yaw, waterZ);
        angler = SeeAround(angler);
        _anglers.Set(angler);
        SendPage(angler);
    }

    private void OnCast(PlayerId sender, Vector3 point)
    {
        if (!online.TryGet(sender, out var player) || !_anglers.TryGet(player, out var angler) || angler.Line.Stage != FishingStage.Aim)
            return;

        var reach = Vector2.Distance(new Vector2(point.X, point.Y), new Vector2(angler.Stand.X, angler.Stand.Y));
        if (!float.IsFinite(point.X) || !float.IsFinite(point.Y) || !float.IsFinite(point.Z)
            || reach > FishingReach.Farthest + FishingReach.Slack || Math.Abs(point.Z - angler.WaterZ) > FishingReach.SameWater)
        {
            logger.LogWarning("{Player} cast to {Point}, {Reach} cm away, out of the rod's reach or off the water at {WaterZ}.",
                player.Ref, point, reach, angler.WaterZ);
            return;
        }

        var now = clock.MonotonicMs;
        angler.Line.Cast(now);
        angler = ShowFloat(angler with { Bait = point }, now);
        Splash(angler, FishingSplashKind.Cast, point);
        _anglers.Set(angler);
        SendPage(angler);
    }

    private void OnSpace(PlayerId sender, bool down, int reactionMs)
    {
        if (!online.TryGet(sender, out var player) || !_anglers.TryGet(player, out var angler))
            return;

        var now = clock.MonotonicMs;
        var change = down ? angler.Line.Press(now, reactionMs) : angler.Line.Release(now);
        Apply(angler, change, now);
    }

    private void OnStopAsked(PlayerId sender)
    {
        if (online.TryGet(sender, out var player))
            End(player, notice: null);
    }

    private void OnFroze(OnlinePlayer player) => End(player, notice: null);

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
            LineChange.None or LineChange.Cast => angler,
            LineChange.Nibble => angler with { Nibbles = angler.Nibbles + 1, DipEndsAtMs = now + (long)FishingRules.NibbleDips.TotalMilliseconds },
            LineChange.Bite => Bitten(angler),
            LineChange.Hooked => angler with { NextThrashAtMs = now + (long)FishingRules.ThrashSoonest.TotalMilliseconds },
            LineChange.Bend => Thrash(angler, now),
            LineChange.Caught => Land(angler),
            LineChange.LostEarly => Lose(angler, FishingOutcome.LostEarly),
            LineChange.LostNibble => Lose(angler, FishingOutcome.LostNibble),
            LineChange.LostLate => Lose(angler, FishingOutcome.LostLate),
            LineChange.LostSnapped => Lose(angler, FishingOutcome.LostSnapped),
            LineChange.LostSlack => Lose(angler, FishingOutcome.LostSlack),
            LineChange.Ready => Aim(angler),
        };
        next = ShowFloat(next, now);
        _anglers.Set(next);
        if (change != LineChange.None)
            SendPage(next);
    }

    private Angler Aim(Angler angler)
    {
        rpc.SendFishingAim(angler.Player.Id, angler.Yaw, angler.WaterZ);
        return angler;
    }

    private Angler Bitten(Angler angler)
    {
        rpc.SendFishingBite(angler.Player.Id);
        if (angler.Bait is { } bait)
            Splash(angler, FishingSplashKind.Bite, bait);

        return angler with { Bites = angler.Bites + 1 };
    }

    private Angler Thrash(Angler angler, long now)
    {
        if (now < angler.NextThrashAtMs || angler.FloatShown is not { } shown)
            return angler;

        Splash(angler, FishingSplashKind.Thrash, shown with { Z = angler.WaterZ });
        return angler with { NextThrashAtMs = now + (long)FishingRules.ThrashWait(_random.NextDouble()).TotalMilliseconds };
    }

    // A find in some of the landed bites, a fish in the rest, into the pack.
    private Angler Land(Angler angler)
    {
        var player = angler.Player;
        if (angler.FloatShown is { } shown)
            Splash(angler, FishingSplashKind.Catch, shown with { Z = angler.WaterZ });

        var number = (angler.Outcome?.Number ?? 0) + 1;
        FishingOutcomeView outcome;
        bool given;
        if (FishingRules.FindOf(_random.NextDouble()) is { } find)
        {
            var name = ItemCatalog.Shipped.FindByForm(find.Item.Value)?.Name
                       ?? throw new InvalidOperationException($"The find {find.Kind} is no item of the game's the mode knows.");
            given = items.TryGive(player, find);
            outcome = new FishingOutcomeView(number, FishingOutcome.Found, name, LengthCm: 0, Grams: 0, Trophy: false);
            chat.Notify(player, FishingText.Found(name));
        }
        else
        {
            var fish = FishingRules.FishOf(_random.NextDouble());
            var share = Math.Pow(_random.NextDouble(), FishingRules.SizeSkew);
            var length = fish.LengthAt(share);
            var grams = fish.GramsAt(length);
            given = items.TryGive(player, fish);
            outcome = new FishingOutcomeView(number, FishingOutcome.Caught, fish.ItemName, length, grams, share >= FishingRules.TrophyFrom);
            chat.Notify(player, FishingText.Caught(fish.ItemName, length, grams));
        }

        if (!given)
        {
            logger.LogError("{Player} landed {Catch}, which could not be put in their pack.", player.Ref, outcome.Name);
            chat.Notify(player, FishingText.NotGiven);
        }

        return angler with { Bait = null, Outcome = outcome };
    }

    private static Angler Lose(Angler angler, FishingOutcome why) =>
        angler with { Bait = null, Outcome = new FishingOutcomeView((angler.Outcome?.Number ?? 0) + 1, why, Name: null, LengthCm: 0, Grams: 0, Trophy: false) };

    // Where the games near should draw the float now, told only when it moved.
    private Angler ShowFloat(Angler angler, long now)
    {
        var at = FindFloat(angler, now);
        if (at == angler.FloatShown)
            return angler;

        foreach (var seer in angler.Seers)
        {
            if (at is { } point)
                rpc.SendFishingFloat(seer, angler.Player.Id.RawValue, point.X, point.Y, point.Z);
            else
                rpc.SendFishingFloatGone(seer, angler.Player.Id.RawValue);
        }

        return angler with { FloatShown = at };
    }

    // Riding on the water, dipped by a nibble, pulled under by the bite, and dragged aside by the fish while landing.
    private static Vector3? FindFloat(Angler angler, long now)
    {
        if (angler.Bait is not { } bait)
            return null;

        return angler.Line.Stage switch
        {
            FishingStage.Waiting => bait with { Z = now < angler.DipEndsAtMs ? bait.Z - FishingRules.NibbleDip : bait.Z + FishingRules.FloatRides },
            FishingStage.Bite => bait with { Z = bait.Z - FishingRules.FloatUnder },
            FishingStage.Landing => Drag(angler, bait),
            FishingStage.Aim or FishingStage.Rest => null,
        };
    }

    private static Vector3 Drag(Angler angler, Vector3 bait)
    {
        var aside = FishingShot.Right(angler.Yaw) * (float)angler.Line.Pull * FishingRules.FishDragsAside;
        return new Vector3(bait.X + aside.X, bait.Y + aside.Y, bait.Z - FishingRules.FloatUnder);
    }

    private void Splash(Angler angler, FishingSplashKind kind, Vector3 at)
    {
        var name = kind.ToString();
        foreach (var seer in angler.Seers)
            rpc.SendFishingSplash(seer, name, at.X, at.Y, at.Z);
    }

    // Who is near now: a newcomer is told of the rod and the float, one who went away that they are gone.
    private Angler SeeAround(Angler angler)
    {
        var near = proximity.FindAround(angler.Player.Id, FishingRules.SeenWithin).ToHashSet();
        var id = angler.Player.Id.RawValue;
        foreach (var seer in near.Except(angler.Seers))
        {
            rpc.SendAnglerShown(seer, id, true);
            if (angler.FloatShown is { } shown)
                rpc.SendFishingFloat(seer, id, shown.X, shown.Y, shown.Z);
        }

        foreach (var gone in angler.Seers.Except(near))
        {
            rpc.SendFishingFloatGone(gone, id);
            rpc.SendAnglerShown(gone, id, false);
        }

        return angler with { Seers = near };
    }

    // Stopped from the wheel or the keys, moved off, held by the staff: the character is free again, unless the staff
    // hold it or it flies.
    private void End(OnlinePlayer player, Phrase? notice)
    {
        _looks.Forget(player);
        if (!_anglers.Remove(player, out var angler))
            return;

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
            rpc.SendFishingFloatGone(seer, id);
            rpc.SendAnglerShown(seer, id, false);
        }
    }

    private void SendPage(Angler angler)
    {
        var line = angler.Line;
        var view = new FishingView(line.Stage, angler.Nibbles, angler.Bites, Math.Round(line.Bend, 3), line.Lifting, Math.Round(line.Tired, 3),
            FishingRules.SafeFrom, FishingRules.SafeTo, (int)FishingRules.StrikeWindow.TotalMilliseconds, angler.Outcome);
        rpc.SendFishingPage(angler.Player.Id, JsonSerializer.Serialize(view, FishingJson.Default.FishingView));
    }
}
