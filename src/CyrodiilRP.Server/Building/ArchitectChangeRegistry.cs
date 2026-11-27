namespace CyrodiilRP.Server.Building;

// The last changes of each architect, by their account, the newest last, for Ctrl+Z. Kept through a relog, not a restart.
// Update thread only.
internal sealed class ArchitectChangeRegistry
{
    public const int MostChanges = 20;

    private readonly Dictionary<string, List<ArchitectChange>> _changes = new(StringComparer.Ordinal);

    // A move of the piece the newest change moved already adds nothing: undone, the piece goes back to where the first stood.
    public void Record(string architect, ArchitectChange change)
    {
        if (!_changes.TryGetValue(architect, out var list))
            _changes[architect] = list = [];

        if (change.Kind == ArchitectChangeKind.Moved && list.Count > 0 && list[^1] is { Kind: ArchitectChangeKind.Moved } last
            && last.PieceId == change.PieceId)
            return;

        list.Add(change);
        if (list.Count > MostChanges)
            list.RemoveAt(0);
    }

    // The newest change on the plot, taken off the list.
    public bool TryTake(string architect, long plotId, out ArchitectChange change)
    {
        change = null!;
        if (!_changes.TryGetValue(architect, out var list))
            return false;

        var at = list.FindLastIndex(each => each.PlotId == plotId);
        if (at < 0)
            return false;

        change = list[at];
        list.RemoveAt(at);
        return true;
    }

    // A piece placed again has a new id, which the older changes to it now name.
    public void Renumber(string architect, long oldId, long newId)
    {
        if (!_changes.TryGetValue(architect, out var list))
            return;

        for (var index = 0; index < list.Count; index++)
        {
            if (list[index].PieceId == oldId)
                list[index] = list[index] with { PieceId = newId };
        }
    }

    public void ForgetPlot(long plotId)
    {
        foreach (var list in _changes.Values)
            list.RemoveAll(each => each.PlotId == plotId);
    }
}
