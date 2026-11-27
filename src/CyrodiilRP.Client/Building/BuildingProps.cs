using System.Numerics;
using CyrodiilRP.Common.Building;
using Microsoft.Extensions.Logging;
using OblivionMP.SDK;
using OblivionMP.SDK.Common.Props;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client.Building;

// The plots' signs and the pieces on them in the character's area, as meshes this game alone stands: the server says
// which, and only for the area the character is in. A mesh this game stands stays through a load, so after every new pawn
// the server is asked again, and what it no longer lists comes down. A piece of furniture or a container is the game's own
// object, which the game shows: it is only known here by its ref id. Architect mode hides a mesh while it is carried. A
// piece of the game's light forms stands a point light where its flame is (GameLights).
[Service]
internal sealed partial class BuildingProps(ILocalProps props, PawnWatch pawn, RpcHandlers rpc, ILogger logger)
{
    // A plot's first tile x and y, width, depth, ground, height, and its sign's x, y, z and yaw.
    private const int NumbersPerPlot = 10;

    // A piece's x, y, z, pitch, yaw, roll and scale.
    private const int NumbersPerPiece = 7;

    // A piece's light casts shadows, so it stays inside the walls it stands in.
    private const bool LightShadows = true;

    private readonly Dictionary<long, StandingPlot> _plots = [];

    private readonly Dictionary<long, StandingPiece> _pieces = [];

    // The pawn (PawnWatch.Number) the server was last asked for.
    private int _askedFor;

    public IEnumerable<long> PlotIds => _plots.Keys;

    // The asset the plots' signs stand as, null until the server sent the plots here.
    public string? SignAsset { get; private set; }

    // The highest id of the pieces here, so a piece placed after it is told from one that stood there already.
    public long HighestPieceId => _pieces.Count == 0 ? 0 : _pieces.Keys.Max();

    public bool TryFindSign(long plotId, out Vector3 position)
    {
        position = default;
        if (!_plots.TryGetValue(plotId, out var plot))
            return false;

        position = plot.Sign.Position;
        return true;
    }

    public bool TryFindPlot(long id, out PlotBox box)
    {
        box = default;
        if (!_plots.TryGetValue(id, out var plot))
            return false;

        box = plot.Box;
        return true;
    }

    public bool TryFindPiece(LocalPropHandle handle, out long id)
    {
        foreach (var (pieceId, piece) in _pieces)
        {
            if (piece.Handle == handle)
            {
                id = pieceId;
                return true;
            }
        }

        id = 0;
        return false;
    }

    // Raised once the server's pieces here are stood, so what points at one finds it again.
    public event Action? Changed;

    public bool TryFindHandle(long id, out LocalPropHandle handle)
    {
        handle = default;
        if (!_pieces.TryGetValue(id, out var piece) || piece.Handle is not { } standing)
            return false;

        handle = standing;
        return true;
    }

    public bool HoldsHandle(LocalPropHandle handle) => _pieces.Values.Any(piece => piece.Handle == handle);

    // A piece of this asset standing at the spot with an id above the one given, as the server stood it: the one just
    // placed there, even over a copy of itself.
    public bool TryFindAt(string asset, Vector3 position, float within, long above, out long id)
    {
        foreach (var (pieceId, piece) in _pieces)
        {
            if (pieceId > above && piece.Prop.Asset == asset && Vector3.Distance(piece.Prop.Position, position) <= within)
            {
                id = pieceId;
                return true;
            }
        }

        id = 0;
        return false;
    }

    public bool TryFindByReference(uint refId, out long id)
    {
        foreach (var (pieceId, piece) in _pieces)
        {
            if (piece.RefId == refId && refId != 0)
            {
                id = pieceId;
                return true;
            }
        }

        id = 0;
        return false;
    }

    public bool TryFindPiece(long id, out Prop piece)
    {
        piece = _pieces.TryGetValue(id, out var standing) ? standing.Prop : default;
        return standing is not null;
    }

    // Taken down while architect mode carries it, so the line under the mouse passes through it.
    public void Hide(long id)
    {
        if (!_pieces.TryGetValue(id, out var piece) || piece.Handle is null)
            return;

        _pieces[id] = TakeDown(piece);
    }

    // Stood again where the server last said, after a carry.
    public void Show(long id)
    {
        if (_pieces.TryGetValue(id, out var piece) && piece.Handle is null && piece.RefId == 0)
            _pieces[id] = Stand(piece);
    }

    private sealed record StandingPlot(PlotBox Box, Prop Sign, LocalPropHandle Handle);

    // Handle: null while architect mode carries it, and for the game's own object (RefId). Light: the point light of a game
    // light form, while it stands.
    private sealed record StandingPiece(Prop Prop, LocalPropHandle? Handle, uint RefId, LocalPropHandle? Light);

    private StandingPiece Stand(StandingPiece piece) =>
        piece with { Handle = props.Place(piece.Prop, collides: true), Light = PlaceLight(piece.Prop) };

    private StandingPiece TakeDown(StandingPiece piece)
    {
        if (piece.Handle is { } handle)
            props.Remove(handle);
        if (piece.Light is { } light)
            props.Remove(light);
        return piece with { Handle = null, Light = null };
    }

    private LocalPropHandle? PlaceLight(Prop prop)
    {
        if (!GameLights.TryRead(prop.Asset, out var light))
            return null;

        var flame = props.TryMeasure(prop.Asset, prop.Scale, out var bounds)
            ? GameLights.FlameOf(light, prop.Position, prop.Rotation.Y, bounds.Min, bounds.Max)
            : prop.Position;
        return props.PlaceLight(new PropLight(flame, light.Red, light.Green, light.Blue, light.Candelas, light.Radius, LightShadows));
    }

    private void Start()
    {
        rpc.PlotsArrived += OnPlotsArrived;
        rpc.PiecesArrived += OnPiecesArrived;
    }

    private void Stop()
    {
        rpc.PlotsArrived -= OnPlotsArrived;
        rpc.PiecesArrived -= OnPiecesArrived;
        foreach (var plot in _plots.Values)
            props.Remove(plot.Handle);

        foreach (var piece in _pieces.Values)
            TakeDown(piece);

        _plots.Clear();
        _pieces.Clear();
    }

    private void Update()
    {
        if (!pawn.InWorld || pawn.Number == _askedFor)
            return;

        _askedFor = pawn.Number;
        rpc.SendAskBuilding();
    }

    private void OnPlotsArrived(string signAsset, long[] ids, float[] numbers)
    {
        SignAsset = signAsset;
        if (numbers.Length != ids.Length * NumbersPerPlot)
        {
            logger.LogError("[CyrodiilRP] The server sent {Count} numbers for {Plots} plots here, which is not {PerPlot} a plot.",
                numbers.Length, ids.Length, NumbersPerPlot);
            return;
        }

        var wanted = new Dictionary<long, StandingPlot>();
        for (var index = 0; index < ids.Length; index++)
        {
            var at = index * NumbersPerPlot;
            var box = new PlotBox((int)numbers[at], (int)numbers[at + 1], (int)numbers[at + 2], (int)numbers[at + 3], numbers[at + 4], numbers[at + 5]);
            var sign = new Prop(signAsset, new Vector3(numbers[at + 6], numbers[at + 7], numbers[at + 8]), new Vector3(0f, numbers[at + 9], 0f), 1f);
            wanted[ids[index]] = new StandingPlot(box, sign, default);
        }

        foreach (var (id, plot) in _plots.ToList())
        {
            if (wanted.TryGetValue(id, out var still) && still.Sign == plot.Sign)
            {
                _plots[id] = plot with { Box = still.Box };
                continue;
            }

            props.Remove(plot.Handle);
            _plots.Remove(id);
        }

        foreach (var (id, plot) in wanted.Where(pair => !_plots.ContainsKey(pair.Key)))
            _plots[id] = plot with { Handle = props.Place(plot.Sign, collides: true) };
    }

    private void OnPiecesArrived(long[] ids, string[] assets, float[] numbers, long[] references)
    {
        if (assets.Length != ids.Length || references.Length != ids.Length || numbers.Length != ids.Length * NumbersPerPiece)
        {
            logger.LogError("[CyrodiilRP] The server sent {Assets} assets and {Count} numbers for {Pieces} pieces here, which is not one asset and {PerPiece} numbers a piece.",
                assets.Length, numbers.Length, ids.Length, NumbersPerPiece);
            return;
        }

        var wanted = new Dictionary<long, StandingPiece>();
        for (var index = 0; index < ids.Length; index++)
        {
            var at = index * NumbersPerPiece;
            var prop = new Prop(assets[index], new Vector3(numbers[at], numbers[at + 1], numbers[at + 2]),
                new Vector3(numbers[at + 3], numbers[at + 4], numbers[at + 5]), numbers[at + 6]);
            wanted[ids[index]] = new StandingPiece(prop, null, (uint)references[index], null);
        }

        foreach (var (id, piece) in _pieces.ToList())
        {
            if (wanted.TryGetValue(id, out var still) && still.Prop == piece.Prop && still.RefId == piece.RefId)
                continue;

            TakeDown(piece);
            _pieces.Remove(id);
        }

        foreach (var (id, piece) in wanted.Where(pair => !_pieces.ContainsKey(pair.Key)))
            _pieces[id] = piece.RefId == 0 ? Stand(piece) : piece;

        Changed?.Invoke();
    }
}
