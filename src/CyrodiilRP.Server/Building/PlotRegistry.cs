using System.Diagnostics.CodeAnalysis;
using CyrodiilRP.Common.Building;
using ReadyM.Api.Idents;

namespace CyrodiilRP.Server.Building;

// The plots, by id. Nothing here waits for the database: Plots writes first and tells this after. Update thread only.
internal sealed class PlotRegistry
{
    private readonly Dictionary<long, PlotRecord> _plots = [];

    public IReadOnlyCollection<PlotRecord> All => _plots.Values;

    public bool TryFind(long id, [NotNullWhen(true)] out PlotRecord? plot) => _plots.TryGetValue(id, out plot);

    public IReadOnlyList<PlotRecord> ListIn(AreaId area) => _plots.Values.Where(p => p.AreaId == area).OrderBy(p => p.Id).ToList();

    // The plot whose tiles hold the point, of those in the area.
    public PlotRecord? FindAt(AreaId area, float x, float y) =>
        _plots.Values.Where(p => p.AreaId == area && p.Box.Contains(x, y)).OrderBy(p => p.Id).FirstOrDefault();

    public bool Overlaps(AreaId area, PlotBox box) => _plots.Values.Any(p => p.AreaId == area && p.Box.Overlaps(box));

    // What was read at start. A plot made while the read was on its way is here already.
    public void Load(IEnumerable<PlotRecord> plots)
    {
        foreach (var plot in plots)
            _plots.TryAdd(plot.Id, plot);
    }

    public void Add(PlotRecord plot) => _plots.Add(plot.Id, plot);

    public bool Remove(long id) => _plots.Remove(id);
}
