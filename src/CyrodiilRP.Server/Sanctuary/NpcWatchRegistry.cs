namespace CyrodiilRP.Server.Sanctuary;

// The NPCs inside the Sanctuary by ref id. One no longer seen there is dropped.
internal sealed class NpcWatchRegistry
{
    private readonly Dictionary<uint, NpcWatch> _watches = [];
    private readonly HashSet<uint> _seen = [];
    private readonly List<uint> _gone = [];

    public IEnumerable<NpcWatch> All => _watches.Values;

    // Seen this pass: kept by the next Sweep.
    public NpcWatch Attach(uint refId)
    {
        _seen.Add(refId);
        if (!_watches.TryGetValue(refId, out var watch))
            _watches[refId] = watch = new NpcWatch(refId);

        return watch;
    }

    // Drops every NPC not attached since the last sweep, each handed to closing first.
    public void Sweep(Action<NpcWatch> closing)
    {
        _gone.Clear();
        _gone.AddRange(_watches.Keys.Where(refId => !_seen.Contains(refId)));
        foreach (var refId in _gone)
        {
            closing(_watches[refId]);
            _watches.Remove(refId);
        }

        _seen.Clear();
    }

    public void Clear(Action<NpcWatch> closing)
    {
        foreach (var watch in _watches.Values)
            closing(watch);

        _watches.Clear();
        _seen.Clear();
    }
}
