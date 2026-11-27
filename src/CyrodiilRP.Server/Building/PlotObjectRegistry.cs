using System.Diagnostics.CodeAnalysis;

namespace CyrodiilRP.Server.Building;

// The pieces standing on the plots, by id. Nothing here waits for the database: Plots writes first and tells this after.
// Update thread only.
internal sealed class PlotObjectRegistry
{
    private readonly Dictionary<long, PlotObjectRecord> _objects = [];

    public bool TryFind(long id, [NotNullWhen(true)] out PlotObjectRecord? piece) => _objects.TryGetValue(id, out piece);

    public IReadOnlyList<PlotObjectRecord> ListOn(long plotId) => _objects.Values.Where(o => o.PlotId == plotId).OrderBy(o => o.Id).ToList();

    public int CountOn(long plotId) => _objects.Values.Count(o => o.PlotId == plotId);

    // What was read at start, of the plots that are here. A piece placed while the read was on its way is here already.
    public void Load(IEnumerable<PlotObjectRecord> pieces, Func<long, bool> plotIsHere)
    {
        foreach (var piece in pieces.Where(p => plotIsHere(p.PlotId)))
            _objects.TryAdd(piece.Id, piece);
    }

    public void Put(PlotObjectRecord piece) => _objects[piece.Id] = piece;

    // The ref ids of the game's objects the pieces are.
    public IReadOnlySet<uint> ListRefIds() => _objects.Values.Where(o => o.RefId != 0).Select(o => o.RefId).ToHashSet();

    public bool Remove(long id) => _objects.Remove(id);

    // Its pieces, which went with it.
    public IReadOnlyList<PlotObjectRecord> RemovePlot(long plotId)
    {
        var gone = ListOn(plotId);
        foreach (var piece in gone)
            _objects.Remove(piece.Id);

        return gone;
    }
}
