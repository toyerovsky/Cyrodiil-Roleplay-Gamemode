namespace CyrodiilRP.Server.Gambling;

internal sealed class GamblingPicks
{
    public const int RelayMs = 100;
    private readonly int[] _masks = [0, 0];
    private bool _pending;
    private long _nextRelay;
    public int[] Masks => _masks.ToArray();

    public void Select(int seat, int mask)
    {
        if (seat is < GamblingMatch.First or > GamblingMatch.Second || mask is < 0 or > GamblingMatch.AllDiceMask)
            throw new ArgumentOutOfRangeException(nameof(mask));
        _masks[seat] = mask;
        _pending = true;
    }

    public bool Flush(long now)
    {
        if (!_pending || now < _nextRelay) return false;
        _pending = false;
        _nextRelay = now + RelayMs;
        return true;
    }

    public void Clear()
    {
        Array.Clear(_masks);
        _pending = false;
        _nextRelay = 0;
    }
}
