using System.Numerics;
using Microsoft.Extensions.Logging;
using OblivionMP.SDK;
using OblivionMP.SDK.Common.Props;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client.Notices;

// The notice boards of the character's area, as meshes this game alone stands: the server says which, and only for the
// area the character is in. A mesh this game stands stays through a load, so after every new pawn the server is asked
// again, and what it no longer lists comes down.
[Service]
internal sealed partial class BoardProps(ILocalProps props, PawnWatch pawn, RpcHandlers rpc, ILogger logger)
{
    // x, y, z and the yaw.
    private const int NumbersPerBoard = 4;

    private readonly Dictionary<BoardSpot, LocalPropHandle> _standing = [];

    // The pawn (PawnWatch.Number) the server was last asked for.
    private int _askedFor;

    private readonly record struct BoardSpot(string Asset, Vector3 Position, float Yaw);

    private void Start() => rpc.NoticeBoardsArrived += OnArrived;

    private void Stop()
    {
        rpc.NoticeBoardsArrived -= OnArrived;
        foreach (var handle in _standing.Values)
            props.Remove(handle);

        _standing.Clear();
    }

    private void Update()
    {
        if (!pawn.InWorld || pawn.Number == _askedFor)
            return;

        _askedFor = pawn.Number;
        rpc.SendAskNoticeBoards();
    }

    private void OnArrived(string asset, float[] spots)
    {
        if (spots.Length % NumbersPerBoard != 0)
        {
            logger.LogError("[CyrodiilRP] The server sent {Count} numbers for the notice boards here, which is not {PerBoard} a board.",
                spots.Length, NumbersPerBoard);
            return;
        }

        var wanted = new HashSet<BoardSpot>();
        for (var at = 0; at < spots.Length; at += NumbersPerBoard)
            wanted.Add(new BoardSpot(asset, new Vector3(spots[at], spots[at + 1], spots[at + 2]), spots[at + 3]));

        foreach (var (spot, handle) in _standing.Where(pair => !wanted.Contains(pair.Key)).ToList())
        {
            props.Remove(handle);
            _standing.Remove(spot);
        }

        foreach (var spot in wanted.Where(spot => !_standing.ContainsKey(spot)))
            _standing[spot] = props.Place(new Prop(spot.Asset, spot.Position, new Vector3(0f, spot.Yaw, 0f), 1f), collides: true);
    }
}
