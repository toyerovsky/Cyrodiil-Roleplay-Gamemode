using System.Security.Cryptography;

namespace CyrodiilRP.Server.Gambling;

internal sealed class GamblingThrows
{
    public const int AnimationMs = 3000;
    private readonly List<GamblingThrow> _events = [];
    private int _published;
    private long _startedAt;
    public bool Active { get; private set; }
    public GamblingThrow? Current => Active ? _events[_published - 1] : null;
    public GamblingThrow[] Published => _events.Take(_published).ToArray();
    public long Elapsed(long now) => Active ? Math.Max(0, now - _startedAt) : 0;

    public int[] Hand(int seat, int round, int replay)
    {
        int[] hand = [];
        foreach (var thrown in Published.Where(thrown => thrown.Seat == seat && thrown.Round == round && thrown.Replay == replay))
        {
            hand = thrown.Kind switch
            {
                GamblingThrowKind.Initial => new int[thrown.Values.Length],
                GamblingThrowKind.Reroll => hand,
            };
            for (var index = 0; index < thrown.Indices.Length; index++) hand[thrown.Indices[index]] = thrown.Values[index];
        }
        return hand;
    }

    public void Add(long match, int seat, int round, int replay, GamblingThrowKind kind, int[] indices, int[] values, string set)
    {
        if (indices.Length != values.Length || indices.Length == 0)
            throw new ArgumentException("A throw needs one value for each moving die.");
        _events.Add(new GamblingThrow(_events.Count + 1, match, seat, round, replay, kind,
            indices.ToArray(), values.ToArray(), RandomNumberGenerator.GetInt32(1, int.MaxValue), set));
    }

    public bool Start(long now)
    {
        if (Active) throw new InvalidOperationException("A throw is already playing.");
        if (_published == _events.Count) return false;
        _published++;
        _startedAt = now;
        Active = true;
        return true;
    }

    public bool Advance(long now)
    {
        if (!Active || Elapsed(now) < AnimationMs) return false;
        Active = false;
        Start(now);
        return true;
    }

    public void Stop() => Active = false;
}
