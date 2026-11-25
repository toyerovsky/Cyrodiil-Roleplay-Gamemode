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
    private long _lastTickMs;
    private long _nextPullAtMs;
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

    // How near a slack line is to throwing the hook, 0 to 1.
    public double SlackShare => Math.Min(1.0, (double)_slackMs / Ms(FishFightRules.SlackLoses));

    public TimeSpan StrikeWindow => _cast?.StrikeWindow ?? FishingRules.StrikeWindow;

    public FightFeel Feel => _cast is { } cast ? FishFightRules.FeelOf(cast.Fight.Strength, cast.SizeShare) : FightFeel.Small;

    public LineChange Cast(long now, LineCast cast)
    {
        if (Stage != FishingStage.Aim)
            return LineChange.None;

        if (!(cast.WaitFactor > 0 && cast.LineStrength > 0 && cast.ReachCm > 0 && cast.DistanceCm >= 0 && cast.SizeShare is >= 0 and <= 1))
            throw new ArgumentOutOfRangeException(nameof(cast), cast, "A cast waits, holds and reaches more than nothing.");

        _cast = cast;
        Stage = FishingStage.Waiting;
        _biteAtMs = now + (long)(Ms(FishingRules.BiteWait(roll())) * cast.WaitFactor);
        _lastNibbleMs = null;
        _reel.Reset(false);
        _lean.Reset(0);
        ScheduleNibbles(now);
        var first = _nibbles.TryPeek(out var nibble) ? nibble : _biteAtMs;
        _approachAtMs = Math.Min(first, Math.Max(now + Ms(FishingRules.ApproachSoonest), first - Ms(FishingRules.ApproachBefore)));
        return LineChange.Cast;
    }

    // The key went down. reactionMs: how long after the bite the player's game heard it, for a strike.
    public LineChange Press(long now, int reactionMs) => Stage switch
    {
        FishingStage.Waiting => Lose(now, SpookedByNibble(now) ? LineChange.LostNibble : LineChange.LostEarly),
        FishingStage.Bite => Strike(now, reactionMs),
        FishingStage.Fight => Hold(_reel, now, true),
        FishingStage.Aim or FishingStage.Rest => LineChange.None,
    };

    public LineChange Release(long now) => Stage == FishingStage.Fight ? Hold(_reel, now, false) : LineChange.None;

    // -1 to the left, 0 straight, 1 to the right, from the bite on: the rod may already lean as the fish is hooked.
    public LineChange LeanTo(long now, int lean)
    {
        if (lean is < LeanLeft or > LeanRight)
            throw new ArgumentOutOfRangeException(nameof(lean), lean, "A rod leans -1, 0 or 1.");

        return Stage is FishingStage.Bite or FishingStage.Fight ? Hold(_lean, now, lean) : LineChange.None;
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
        DistanceCm = Current.DistanceCm;
        DriftCm = 0;
        Pull = FishPull.Rest;
        _reel.Reset(false);
        _lastTickMs = now;
        _nextPullAtMs = now;
        _slackMs = 0;
        return LineChange.Hooked;
    }

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
        var turned = now >= _nextPullAtMs ? Turn(now, NextPull()) : LineChange.Fought;
        _reel.Settle(now);
        _lean.Settle(now);
        var pullBefore = Pull;
        var stepMs = Ms(FishFightRules.LongestStep);
        for (var left = elapsed; left > 0; left -= stepMs)
        {
            var outcome = Step(now, Math.Min(left, stepMs));
            if (outcome != LineChange.None)
                return outcome;
        }

        return turned == LineChange.Fought && Pull != pullBefore ? LineChange.Turned : turned;
    }

    private LineChange Step(long now, long stepMs)
    {
        var cast = Current;
        var seconds = stepMs / MsPerSecond;
        var effort = cast.Fight.Strength * FishFightRules.SizeFactor(cast.SizeShare)
                     * (FishFightRules.EffortSpent + (1 - FishFightRules.EffortSpent) * Stamina);
        var running = Pull != FishPull.Rest;
        var against = IsAgainst();
        var give = running || Reeling ? FishFightRules.Give : FishFightRules.SlackGive;
        var target = TensionAsked(effort, running, against, cast.LineStrength);
        Tension = Math.Max(0, Tension + (target - Tension) * Math.Min(1, give * seconds));
        if (Tension >= FishFightRules.Snaps)
        {
            Tension = FishFightRules.Snaps;
            return Lose(now, LineChange.LostSnapped);
        }

        _slackMs = Tension <= FishFightRules.SlackFrom ? _slackMs + stepMs : 0;
        if (_slackMs >= Ms(FishFightRules.SlackLoses))
            return Lose(now, LineChange.LostSlack);

        var tiring = Tension is >= FishFightRules.TiringFrom and <= FishFightRules.TiringTo ? 1 : FishFightRules.OutOfBandTires;
        Stamina = Math.Max(0, Stamina - seconds / cast.Fight.StaminaSeconds * tiring * (against ? FishFightRules.AgainstTires : 1));

        if (Reeling)
            DistanceCm -= FishFightRules.ReelSpeed * (running ? FishFightRules.ReelInRunShare : 1) * seconds;
        DistanceCm += Pull switch
        {
            FishPull.Away => FishFightRules.AwaySpeed * effort * seconds,
            FishPull.Left or FishPull.Right => FishFightRules.SideSpeed * effort * seconds,
            FishPull.Rest => 0,
        };
        Drift(seconds);

        if (DistanceCm > FishFightRules.RunsOutAt * cast.ReachCm)
            return Lose(now, LineChange.LostRunaway);

        if (DistanceCm > FishFightRules.LandWithin)
            return LineChange.None;

        if (Stamina <= FishFightRules.LandTired)
        {
            Rest(now, FishingRules.NextCastAfterCatch);
            return LineChange.Caught;
        }

        // Too fresh to land: it bolts.
        DistanceCm = Math.Max(DistanceCm, FishFightRules.Closest);
        if (Pull != FishPull.Away)
            Turn(now, FishPull.Away);

        return LineChange.None;
    }

    private double TensionAsked(double effort, bool running, bool against, double line)
    {
        if (!running)
            return Reeling ? FishFightRules.RestHold : 0;

        var pulled = (Pull == FishPull.Away ? FishFightRules.RunAway : FishFightRules.RunSide) * effort;
        if (IsWith())
            pulled += FishFightRules.LeanWith * effort;
        if (Reeling)
            pulled += FishFightRules.ReelRun * effort;

        var asked = FishFightRules.RunBase + (Reeling ? FishFightRules.ReelRunBase : 0) + pulled / line;
        return against ? asked * FishFightRules.LeanAgainstShare : asked;
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

    // Rests grow likelier as the fish tires; a run may bring a jump.
    private FishPull NextPull()
    {
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
        Pull = pull;
        var jumped = pull != FishPull.Rest && roll() < fight.Jumpiness;
        _nextPullAtMs = now + (long)(MsPerSecond * (fight.ChangeSoonestSeconds + (fight.ChangeLatestSeconds - fight.ChangeSoonestSeconds) * roll()));
        if (jumped)
            return LineChange.Jumped;

        return turned ? LineChange.Turned : LineChange.Fought;
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
        Rest(now, FishingRules.NextCastAfterLoss);
        return why;
    }

    private void Rest(long now, TimeSpan pause)
    {
        Stage = FishingStage.Rest;
        _reel.Reset(false);
        _readyAtMs = now + Ms(pause);
    }

    private LineChange Ready()
    {
        Stage = FishingStage.Aim;
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
