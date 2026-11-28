using CyrodiilRP.Common.Fishing;

namespace CyrodiilRP.Server.Fishing;

// One angler's line from the cast to the catch, on a clock in milliseconds the caller passes, every chance from the rolls
// it passes. Every time is kept here and every fight decided here: the player's game only says where the float landed,
// when Space went down and up and which way the rod leans.
internal sealed class FishingLine(Func<double> roll)
{
    private const double MsPerSecond = 1000.0;
    private const int LeanLeft = -1;
    private const int LeanRight = 1;

    private readonly Queue<long> _nibbles = new();
    private readonly PacedInput<bool> _reel = new(false);
    private readonly PacedInput<int> _lean = new(0);
    private LineCast? _cast;
    private long _biteAtMs;
    private long? _approachAtMs;
    private long? _lastNibbleMs;
    private long _readyAtMs;
    private long _restFromMs;
    private bool _showing;
    private long _castAtMs;
    private long _stirredAtMs;
    private long? _twitchedAtMs;
    private bool _provoked;
    private long _lastTickMs;
    private long _nextPullAtMs;
    private long _pullSinceMs;
    private long _hookedAtMs;
    private bool _reeled;
    private bool _jolted;
    private long _slackMs;

    public FishingStage Stage { get; private set; } = FishingStage.Aim;

    // 0 slack, FishFightRules.Snaps the line breaks.
    public double Tension { get; private set; }

    // The fish's: 1 fresh, 0 spent.
    public double Stamina { get; private set; }

    public double DistanceCm { get; private set; }

    // How far the fish dragged the float aside: below 0 to the angler's left, above 0 to the right.
    public double DriftCm { get; private set; }

    public FishPull Pull { get; private set; } = FishPull.Rest;

    public bool Reeling => _reel.Value;

    public int Lean => _lean.Value;

    // How near a line slack now is to throwing the hook, 0 to 1.
    public double SlackShare => Tension <= FishFightRules.SlackFrom ? Math.Min(1.0, (double)_slackMs / Ms(FishFightRules.SlackLoses)) : 0;

    public TimeSpan StrikeWindow => _cast?.StrikeWindow ?? FishingRules.StrikeWindow;

    public FightFeel Feel => _cast is { } cast ? FishFightRules.FeelOf(cast.Fight.Strength, cast.SizeShare) : FightFeel.Small;

    // How far out the float lies while the line waits: where it landed, less what the twitches pulled it in.
    public double FloatDistanceCm => _cast?.DistanceCm ?? 0;

    // Whether the float lies too near for another twitch to pull it in.
    public bool IsPulledIn => _cast is { } cast && cast.DistanceCm - FishingRules.TwitchPull < cast.NearestCm;

    // How long until the water is calm again after the cast or the last twitch.
    public TimeSpan CalmIn(long now) =>
        Stage == FishingStage.Waiting ? TimeSpan.FromMilliseconds(Math.Max(0, _stirredAtMs + Ms(FishingRules.CalmAfter) - now)) : TimeSpan.Zero;

    public LineChange Cast(long now, LineCast cast)
    {
        if (Stage != FishingStage.Aim)
            return LineChange.None;

        if (!(cast.WaitFactor > 0 && cast.LineStrength > 0 && cast.ReachCm > 0 && cast.DistanceCm >= 0 && cast.SizeShare is >= 0 and <= 1
              && cast.NearestCm >= FishingReach.Nearest))
            throw new ArgumentOutOfRangeException(nameof(cast), cast, "A cast waits, holds and reaches more than nothing.");

        _cast = cast;
        Stage = FishingStage.Waiting;
        _biteAtMs = now + (long)(Ms(FishingRules.BiteWait(roll())) * cast.WaitFactor);
        _castAtMs = now;
        _stirredAtMs = now;
        _twitchedAtMs = null;
        _provoked = false;
        _reel.Reset(false);
        _lean.Reset(0);
        ScheduleFish(now, FishingRules.ApproachSoonest);
        return LineChange.Cast;
    }

    // A tug on the bait while the line waits, once the float has landed, which pulls the float in. In calm water it
    // draws the fish, in troubled water it puts them off. With a fish at the float it is a gamble in calm water, the
    // fish takes the bait or bolts, and scares the fish for sure in troubled water. A fish already provoked is left to
    // take the bait.
    public LineChange Twitch(long now)
    {
        if (Stage != FishingStage.Waiting || _provoked || IsPulledIn || now - _castAtMs < Ms(FishingRules.FloatLandsWithin)
            || (_twitchedAtMs is { } before && now - before < Ms(FishingRules.TwitchSoonest))
            || (_lastNibbleMs is { } nibble && now - nibble < Ms(FishingRules.NibbleShields)))
            return LineChange.None;

        var cast = Current;
        var calm = now - _stirredAtMs >= Ms(FishingRules.CalmAfter);
        _stirredAtMs = now;
        _twitchedAtMs = now;
        _cast = cast with { DistanceCm = cast.DistanceCm - FishingRules.TwitchPull };
        // A find comes up without a fish to provoke or scare.
        if (_approachAtMs is null && cast.Fight.Runs)
            return calm && roll() < cast.Twitch.StrikeChance ? Provoke(now) : Spook(now, cast);

        if (!calm)
        {
            Shift(now, 1, Ms(FishingRules.TroubledWaits));
            return LineChange.Troubled;
        }

        var left = _biteAtMs - now;
        var kept = Math.Max(Ms(FishingRules.TwitchWaitAtLeast), (long)(left * (1 - cast.Twitch.Draw)));
        if (kept < left)
            Shift(now, (double)kept / left, 0);

        return LineChange.Drew;
    }

    // The key went down. reactionMs: how long after the bite the player's game heard it, for a strike.
    public LineChange Press(long now, int reactionMs) => Stage switch
    {
        FishingStage.Waiting => Lose(now, SpookedByNibble(now) ? LineChange.LostNibble : LineChange.LostEarly),
        FishingStage.Bite => Strike(now, reactionMs),
        FishingStage.Fight => Hold(_reel, now, true),
        FishingStage.Rest => CastAgain(now),
        FishingStage.Aim => LineChange.None,
    };

    public LineChange Release(long now) => Stage == FishingStage.Fight ? Hold(_reel, now, false) : LineChange.None;

    // -1 to the left, 0 straight, 1 to the right, from the bite on: the rod may already lean as the fish is hooked.
    public LineChange LeanTo(long now, int lean)
    {
        if (lean is < LeanLeft or > LeanRight)
            throw new ArgumentOutOfRangeException(nameof(lean), lean, "A rod leans -1, 0 or 1.");

        return Stage is FishingStage.Bite or FishingStage.Fight ? Hold(_lean, now, lean) : LineChange.None;
    }

    // The catch held up left the hand: the next cast comes as soon as after a loss.
    public void EndShow(long now)
    {
        if (Stage != FishingStage.Rest || !_showing)
            return;

        _showing = false;
        _readyAtMs = Math.Min(_readyAtMs, now + Ms(FishingRules.NextCastAfterLoss));
    }

    public LineChange Tick(long now) => Stage switch
    {
        FishingStage.Waiting => WaitOn(now),
        FishingStage.Bite => now - _biteAtMs > Ms(StrikeWindow + FishingRules.StrikeLatency) ? Lose(now, LineChange.LostLate) : LineChange.None,
        FishingStage.Fight => Fight(now),
        FishingStage.Rest => now >= _readyAtMs ? Ready() : LineChange.None,
        FishingStage.Aim => LineChange.None,
    };

    private LineChange WaitOn(long now)
    {
        if (_approachAtMs is { } approach && now >= approach)
        {
            _approachAtMs = null;
            return LineChange.Approach;
        }

        if (now >= _biteAtMs)
        {
            // The strike's moment counts from the bite as it was shown, a tick after it was due.
            Stage = FishingStage.Bite;
            _provoked = false;
            _biteAtMs = now;
            return LineChange.Bite;
        }

        if (!_nibbles.TryPeek(out var nibble) || now < nibble)
            return LineChange.None;

        _nibbles.Dequeue();
        _lastNibbleMs = now;
        return LineChange.Nibble;
    }

    // In time when it reached the server within the window and the way there, and the player's own game heard it within
    // the window. A reaction a game makes up still has to arrive in time. The press is the strike's: reeling is a new one.
    private LineChange Strike(long now, int reactionMs)
    {
        var window = Ms(StrikeWindow);
        if (now - _biteAtMs > window + Ms(FishingRules.StrikeLatency) || reactionMs > window)
            return Lose(now, LineChange.LostLate);

        Stage = FishingStage.Fight;
        Tension = FishFightRules.TensionAtHook;
        Stamina = 1;
        DistanceCm = Math.Max(Current.DistanceCm, FishFightRules.HookedAtLeast);
        DriftCm = 0;
        Pull = FishPull.Rest;
        _reel.Reset(false);
        _reeled = false;
        _jolted = false;
        _hookedAtMs = now;
        _lastTickMs = now;
        _nextPullAtMs = now;
        _pullSinceMs = now;
        _slackMs = 0;
        return LineChange.Hooked;
    }

    // A press while the catch is held up, once it was shown a while, does not wait the show out.
    private LineChange CastAgain(long now) =>
        _showing && now - _restFromMs >= Ms(FishingRules.CatchShownAtLeast) ? Ready() : LineChange.None;

    private static LineChange Hold<T>(PacedInput<T> input, long now, T value) where T : struct, IEquatable<T>
    {
        input.Set(now, value);
        return LineChange.None;
    }

    // The fish's turn first, then the angler's hands, then the fight worked out in short steps.
    private LineChange Fight(long now)
    {
        var elapsed = now - _lastTickMs;
        if (elapsed <= 0)
            return LineChange.None;

        _lastTickMs = now;
        if (now - _hookedAtMs > Ms(FishFightRules.LongestFight))
            return Lose(now, LineChange.LostSlack);

        var turned = now >= _nextPullAtMs ? Turn(now, NextPull()) : LineChange.Fought;
        _reel.Settle(now);
        _lean.Settle(now);
        Jolt(now);
        var pullBefore = Pull;
        var stepMs = Ms(FishFightRules.LongestStep);
        for (var left = elapsed; left > 0; left -= stepMs)
        {
            var step = Math.Min(left, stepMs);
            var outcome = Step(now, now - left + step, step);
            if (outcome != LineChange.None)
                return outcome;
        }

        return turned == LineChange.Fought && Pull != pullBefore ? LineChange.Turned : turned;
    }

    // A press of the reel into a run jerks the line, so tapping the reel through a run strains it as holding does.
    // Judged at the time of the press, which a late tick hears of only later.
    private void Jolt(long now)
    {
        var pressed = Reeling && !_reeled;
        _reeled = Reeling;
        if (!pressed)
            return;

        var pressedAt = _reel.ChangedAtMs ?? throw new InvalidOperationException("A reel that reels was pressed at some time.");
        if (!IsForced(pressedAt))
            return;

        Tension += _jolted ? FishFightRules.ForceJoltAgain : FishFightRules.ForceJolt;
        _jolted = true;
    }

    private bool IsForced(long at) => Reeling && Pull != FishPull.Rest && at - _pullSinceMs >= Ms(FishFightRules.ForceAfter);

    // now: the tick's time, which an ending is told at. at: the time this step of it ends.
    private LineChange Step(long now, long at, long stepMs)
    {
        var cast = Current;
        var seconds = stepMs / MsPerSecond;
        var effort = cast.Fight.Strength * FishFightRules.SizeFactor(cast.SizeShare)
                     * (FishFightRules.EffortSpent + (1 - FishFightRules.EffortSpent) * Stamina);
        var running = Pull != FishPull.Rest;
        var against = IsAgainst();
        var with = IsWith() && at - _pullSinceMs >= Ms(FishFightRules.ForceAfter);
        var forcing = IsForced(at);
        var target = TensionAsked(effort / cast.LineStrength, running, forcing, against, with);
        Tension = Math.Max(0, Tension + (target - Tension) * Math.Min(1, GiveTowards(target, running, forcing || with) * seconds));
        if (Tension >= FishFightRules.Snaps)
        {
            Tension = FishFightRules.Snaps;
            return Lose(now, LineChange.LostSnapped);
        }

        _slackMs = Tension <= FishFightRules.SlackFrom ? _slackMs + stepMs : Math.Max(0, _slackMs - stepMs);
        if (_slackMs >= Ms(FishFightRules.SlackLoses))
            return Lose(now, LineChange.LostSlack);

        Stamina = Math.Clamp(Stamina - seconds / cast.Fight.StaminaSeconds * Tires(forcing, against, with), 0, 1);

        if (Reeling && !running)
            DistanceCm -= FishFightRules.ReelSpeed * (1 + FishFightRules.ReelSpentBoost * (1 - Stamina)) * seconds;
        DistanceCm += Pull switch
        {
            FishPull.Away => FishFightRules.AwaySpeed * effort * seconds,
            FishPull.Left or FishPull.Right => SideSpeed(against, with) * effort * seconds,
            FishPull.Rest => 0,
        };
        Drift(seconds);

        if (DistanceCm > FishFightRules.RunsOutAt * cast.ReachCm)
            return Lose(now, LineChange.LostRunaway);

        if (DistanceCm > FishFightRules.LandWithin)
            return LineChange.None;

        if (Stamina <= FishFightRules.LandTired)
        {
            Rest(now, FishingRules.NextCastAfterCatch, showing: true);
            return LineChange.Caught;
        }

        // Too fresh to land: it bolts.
        DistanceCm = Math.Max(DistanceCm, FishFightRules.Closest);
        if (Pull != FishPull.Away)
            Turn(now, FishPull.Away);

        return LineChange.None;
    }

    // load: the fish's effort against the line's strength.
    private double TensionAsked(double load, bool running, bool forcing, bool against, bool with)
    {
        if (!running)
            return Reeling ? FishFightRules.RestHold : 0;

        if (forcing)
            return Math.Min(FishFightRules.ForceMost, FishFightRules.ForceBase + FishFightRules.ForcePull * load);

        if (against)
            return FishFightRules.WorkBase + FishFightRules.WorkPull * Math.Min(1, load);

        var pulled = Pull switch
        {
            FishPull.Away => FishFightRules.RunAway,
            FishPull.Left or FishPull.Right => FishFightRules.RunSide,
            FishPull.Rest => throw new InvalidOperationException("A resting fish pulls nothing."),
        };
        return FishFightRules.RunBase + (pulled + (with ? FishFightRules.LeanWith : 0)) * load;
    }

    // A line strained in a run stays strained while the fish runs: the tension falls slower there than it rises. A
    // line the angler strains, by the reel or a rod leant the run's way, rises slower than one the fish pulls, so the
    // angler sees it climb through the red.
    private double GiveTowards(double target, bool running, bool strained)
    {
        if (strained && target > Tension)
            return FishFightRules.ForceGive;

        if (running)
            return target < Tension ? FishFightRules.RunEases : FishFightRules.Give;

        return Reeling ? FishFightRules.Give : FishFightRules.SlackGive;
    }

    // How fast the fish tires, in stamina seconds a second. Below 0 it gets its strength back.
    private double Tires(bool forcing, bool against, bool with)
    {
        if (forcing)
            return FishFightRules.ForcedTires;

        if (with)
            return FishFightRules.WithTires;

        var taut = Tension is >= FishFightRules.TiringFrom and <= FishFightRules.TiringTo ? 1 : FishFightRules.OutOfBandTires;
        if (against)
            return taut * FishFightRules.AgainstTires;

        return Pull switch
        {
            FishPull.Left or FishPull.Right => taut * FishFightRules.LooseTires,
            FishPull.Away or FishPull.Rest => taut,
        };
    }

    private static double SideSpeed(bool against, bool with)
    {
        if (against)
            return 0;

        return with ? FishFightRules.SideSpeed * FishFightRules.WithSpeed : FishFightRules.SideSpeed;
    }

    // Leaning against a run to the left is leaning right, and the other way round. A dive away has no side.
    private bool IsAgainst() => Pull switch
    {
        FishPull.Left => Lean == LeanRight,
        FishPull.Right => Lean == LeanLeft,
        FishPull.Away or FishPull.Rest => false,
    };

    private bool IsWith() => Pull switch
    {
        FishPull.Left => Lean == LeanLeft,
        FishPull.Right => Lean == LeanRight,
        FishPull.Away or FishPull.Rest => false,
    };

    private void Drift(double seconds)
    {
        var toward = Pull switch
        {
            FishPull.Left => -FishFightRules.DriftMost,
            FishPull.Right => FishFightRules.DriftMost,
            FishPull.Away or FishPull.Rest => 0,
        };
        var speed = toward == 0 ? FishFightRules.DriftBack : FishFightRules.DriftSpeed;
        var step = speed * seconds;
        DriftCm = Math.Abs(toward - DriftCm) <= step ? toward : DriftCm + Math.Sign(toward - DriftCm) * step;
    }

    // Rests grow likelier as the fish tires; a run may bring a jump. What does not run only rests.
    private FishPull NextPull()
    {
        if (!Current.Fight.Runs || Stamina <= 0)
            return FishPull.Rest;

        var rest = FishFightRules.RestFresh + (FishFightRules.RestSpent - FishFightRules.RestFresh) * (1 - Stamina);
        var pick = roll();
        if (pick < rest)
            return FishPull.Rest;

        var run = (pick - rest) / (1 - rest);
        if (run < FishFightRules.AwayShare)
            return FishPull.Away;

        return run < FishFightRules.AwayShare + (1 - FishFightRules.AwayShare) / 2 ? FishPull.Left : FishPull.Right;
    }

    // Fought when the fish keeps doing what it did.
    private LineChange Turn(long now, FishPull pull)
    {
        var fight = Current.Fight;
        var turned = pull != Pull;
        if (turned)
            _pullSinceMs = now;
        if (pull == FishPull.Rest)
            _jolted = false;
        Pull = pull;
        var jumped = pull != FishPull.Rest && roll() < fight.Jumpiness;
        _nextPullAtMs = now + (long)(MsPerSecond * (fight.ChangeSoonestSeconds + (fight.ChangeLatestSeconds - fight.ChangeSoonestSeconds) * roll()));
        if (jumped)
            return LineChange.Jumped;

        return turned ? LineChange.Turned : LineChange.Fought;
    }

    private LineChange Provoke(long now)
    {
        _provoked = true;
        _nibbles.Clear();
        _biteAtMs = now + Ms(FishingRules.ProvokedBite(roll()));
        return LineChange.Provoked;
    }

    // A new, shorter wait, and no fish back at the float before the water is calm again.
    private LineChange Spook(long now, LineCast cast)
    {
        _biteAtMs = now + (long)(Ms(FishingRules.SpookWait(roll())) * cast.WaitFactor);
        ScheduleFish(now, FishingRules.CalmAfter);
        return LineChange.Spooked;
    }

    // Every time still to come moves: what is left of the way to it times the share, and later by the delay.
    private void Shift(long now, double share, long delayMs)
    {
        long Moved(long at) => now + (long)((at - now) * share) + delayMs;

        _biteAtMs = Moved(_biteAtMs);
        if (_approachAtMs is { } approach)
            _approachAtMs = Moved(approach);

        var nibbles = _nibbles.Select(Moved).ToList();
        _nibbles.Clear();
        foreach (var nibble in nibbles)
            _nibbles.Enqueue(nibble);
    }

    // The nibbles before the bite, and the fish that shows a while before the first of them or the bite, no sooner
    // than the time given unless they come sooner themselves.
    private void ScheduleFish(long now, TimeSpan showsSoonest)
    {
        _lastNibbleMs = null;
        ScheduleNibbles(now);
        var first = _nibbles.TryPeek(out var nibble) ? nibble : _biteAtMs;
        _approachAtMs = Math.Min(first, Math.Max(now + Ms(showsSoonest), first - Ms(FishingRules.ApproachBefore)));
    }

    private bool SpookedByNibble(long now) => _lastNibbleMs is { } nibble && now - nibble <= Ms(FishingRules.NibbleSpooks);

    // One in each equal part of the wait, so they never bunch up.
    private void ScheduleNibbles(long now)
    {
        _nibbles.Clear();
        var count = Math.Min(FishingRules.MostNibbles, (int)(roll() * (FishingRules.MostNibbles + 1)));
        var from = now + Ms(FishingRules.NibbleSoonest);
        var span = _biteAtMs - Ms(FishingRules.NibbleBeforeBite) - from;
        if (count == 0 || span <= 0)
            return;

        for (var i = 0; i < count; i++)
            _nibbles.Enqueue(from + (long)((i + roll()) * span / count));
    }

    private LineChange Lose(long now, LineChange why)
    {
        _provoked = false;
        Rest(now, FishingRules.NextCastAfterLoss, showing: false);
        return why;
    }

    private void Rest(long now, TimeSpan pause, bool showing)
    {
        Stage = FishingStage.Rest;
        _reel.Reset(false);
        _restFromMs = now;
        _showing = showing;
        _readyAtMs = now + Ms(pause);
    }

    private LineChange Ready()
    {
        Stage = FishingStage.Aim;
        _showing = false;
        Tension = 0;
        Stamina = 0;
        DistanceCm = 0;
        DriftCm = 0;
        Pull = FishPull.Rest;
        _slackMs = 0;
        _lean.Reset(0);
        return LineChange.Ready;
    }

    private LineCast Current => _cast ?? throw new InvalidOperationException("The line has not been cast.");

    private static long Ms(TimeSpan span) => (long)span.TotalMilliseconds;
}
