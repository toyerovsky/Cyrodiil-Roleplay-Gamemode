namespace CyrodiilRP.Server.Fishing;

// One angler's line from the cast to the catch, on a clock in milliseconds the caller passes. Every time is kept here
// and every catch decided here: the player's game only says where the float landed and when the key went down and up.
internal sealed class FishingLine(Func<double> roll)
{
    private const double MsPerSecond = 1000.0;

    private readonly Queue<long> _nibbles = new();
    private long _biteAtMs;
    private long? _lastNibbleMs;
    private long _readyAtMs;
    private long _lastTickMs;
    private long _nextPullAtMs;
    private long? _lastToggleMs;
    private long _neededMs;
    private long _safeMs;
    private long _slackMs;

    public FishingStage Stage { get; private set; } = FishingStage.Aim;

    // The rod's bend while landing: 0 slack, 1 the line breaks.
    public double Bend { get; private set; }

    public bool Lifting { get; private set; }

    // Which way and how hard the fish pulls now, from -1 to 1.
    public double Pull { get; private set; }

    // How tired the fish is: 0 fresh, 1 landed.
    public double Tired => _neededMs == 0 ? 0 : Math.Min(1.0, (double)_safeMs / _neededMs);

    public LineChange Cast(long now)
    {
        if (Stage != FishingStage.Aim)
            return LineChange.None;

        Stage = FishingStage.Waiting;
        _biteAtMs = now + Ms(FishingRules.BiteWait(roll()));
        _lastNibbleMs = null;
        ScheduleNibbles(now);
        return LineChange.Cast;
    }

    // The key went down. reactionMs: how long after the bite the player's game heard it, for a strike.
    public LineChange Press(long now, int reactionMs) => Stage switch
    {
        FishingStage.Waiting => Lose(now, SpookedByNibble(now) ? LineChange.LostNibble : LineChange.LostEarly),
        FishingStage.Bite => Strike(now, reactionMs),
        FishingStage.Landing => Toggle(now, lifting: true),
        FishingStage.Aim or FishingStage.Rest => LineChange.None,
    };

    public LineChange Release(long now) => Stage == FishingStage.Landing ? Toggle(now, lifting: false) : LineChange.None;

    public LineChange Tick(long now) => Stage switch
    {
        FishingStage.Waiting => WaitOn(now),
        FishingStage.Bite => now - _biteAtMs > Ms(FishingRules.StrikeWindow + FishingRules.StrikeLatency) ? Lose(now, LineChange.LostLate) : LineChange.None,
        FishingStage.Landing => Land(now),
        FishingStage.Rest => now >= _readyAtMs ? Ready() : LineChange.None,
        FishingStage.Aim => LineChange.None,
    };

    private LineChange WaitOn(long now)
    {
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
    // the window. A reaction a game makes up still has to arrive in time.
    private LineChange Strike(long now, int reactionMs)
    {
        var window = Ms(FishingRules.StrikeWindow);
        if (now - _biteAtMs > window + Ms(FishingRules.StrikeLatency) || reactionMs > window)
            return Lose(now, LineChange.LostLate);

        Stage = FishingStage.Landing;
        Bend = FishingRules.BendStart;
        Lifting = false;
        Pull = 0;
        _lastTickMs = now;
        _nextPullAtMs = now;
        _safeMs = 0;
        _slackMs = 0;
        _lastToggleMs = null;
        _neededMs = Ms(FishingRules.LandingFor(roll()));
        return LineChange.Hooked;
    }

    private LineChange Toggle(long now, bool lifting)
    {
        if (_lastToggleMs is { } last && now - last < Ms(FishingRules.LiftToggleSoonest))
            return LineChange.None;

        _lastToggleMs = now;
        Lifting = lifting;
        return LineChange.None;
    }

    // Lifting raises the bend, lowering eases it, the fish pulls it either way. In the safe band the fish tires.
    private LineChange Land(long now)
    {
        var elapsed = now - _lastTickMs;
        _lastTickMs = now;
        if (elapsed <= 0)
            return LineChange.None;

        if (now >= _nextPullAtMs)
        {
            Pull = roll() * 2 - 1;
            _nextPullAtMs = now + Ms(FishingRules.FishPullChangesEvery);
        }

        var hands = Lifting ? FishingRules.LiftRise : -FishingRules.LowerFall;
        Bend = Math.Clamp(Bend + (hands + Pull * FishingRules.FishPullMost) * (elapsed / MsPerSecond), 0, 1);
        if (Bend >= 1)
            return Lose(now, LineChange.LostSnapped);

        _slackMs = Bend <= 0 ? _slackMs + elapsed : 0;
        if (_slackMs >= Ms(FishingRules.SlackLoses))
            return Lose(now, LineChange.LostSlack);

        if (Bend >= FishingRules.SafeFrom && Bend <= FishingRules.SafeTo)
            _safeMs += elapsed;
        if (_safeMs < _neededMs)
            return LineChange.Bend;

        Rest(now, FishingRules.NextCastAfterCatch);
        return LineChange.Caught;
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
        Lifting = false;
        _readyAtMs = now + Ms(pause);
    }

    private LineChange Ready()
    {
        Stage = FishingStage.Aim;
        Bend = 0;
        Pull = 0;
        _neededMs = 0;
        _safeMs = 0;
        return LineChange.Ready;
    }

    private static long Ms(TimeSpan span) => (long)span.TotalMilliseconds;
}
