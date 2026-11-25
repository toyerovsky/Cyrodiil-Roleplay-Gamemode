using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace CyrodiilRP.Server.Building;

// The building catalogue by key, in the drawer's order. Update thread only.
internal sealed class PieceRegistry
{
    // A mesh no piece of the game's comes near: a box past it is a client's garbage, not a measurement.
    private const float MostReach = 100_000f;

    private readonly List<string> _order;

    private readonly Dictionary<string, Piece> _pieces;

    public PieceRegistry(IEnumerable<PieceSettings> settings)
    {
        var pieces = settings.Select(s => new Piece(s.Key, s.Name, s.Category, s.Asset, s.Scale, s.Use, s.Object, s.ReadForm(), null)).ToList();
        _order = pieces.Select(p => p.Key).ToList();
        _pieces = pieces.ToDictionary(p => p.Key, StringComparer.Ordinal);
    }

    public IReadOnlyList<Piece> All => _order.Select(key => _pieces[key]).ToList();

    public bool TryFind(string key, [NotNullWhen(true)] out Piece? piece) => _pieces.TryGetValue(key, out piece);

    // The first measurement of a piece stands. False for one outside the catalogue or a box no mesh has.
    public bool Measure(string key, Vector3 min, Vector3 max)
    {
        if (!_pieces.TryGetValue(key, out var piece) || !IsBox(min, max))
            return false;

        if (piece.Bounds is null)
            _pieces[key] = piece with { Bounds = new PieceBounds(min, max) };

        return true;
    }

    private static bool IsBox(Vector3 min, Vector3 max)
    {
        if (!IsFinite(min) || !IsFinite(max))
            return false;

        var reach = Vector3.Max(Vector3.Abs(min), Vector3.Abs(max));
        return min.X <= max.X && min.Y <= max.Y && min.Z <= max.Z
            && reach.X < MostReach && reach.Y < MostReach && reach.Z < MostReach;
    }

    private static bool IsFinite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
