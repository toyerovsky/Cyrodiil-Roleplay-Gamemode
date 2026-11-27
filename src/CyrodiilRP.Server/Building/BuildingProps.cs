using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Network;
using CyrodiilRP.Server.Players;
using Microsoft.Extensions.Logging;
using ReadyM.Api.Idents;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Building;

// Each player's game stands the plots' signs and pieces of the area its character is in, and only those: a prop placed
// for everyone (IProps) names no area, and two areas reuse the same coordinates. A player is told them as they come into
// an area, again as anything there changes, and whenever their game asks.
[Service]
internal sealed partial class BuildingProps(RpcHandlers rpc, OnlinePlayers online, Proximity proximity, Plots plots, Timers timers, ILogger logger)
{
    private const string LookJob = "building.areas";

    // The area whose plots each player's game was last told.
    private readonly Dictionary<OnlinePlayer, AreaId> _told = [];

    private void Start()
    {
        rpc.BuildingAsked += OnAsked;
        online.Left += player => _told.Remove(player);
        plots.AreaChanged += OnAreaChanged;
        timers.RunEvery(LookJob, BuildingRules.LookEvery, Look);
    }

    // An area changes with a load, which the server sees as it happens and the game only once it is over.
    private void Look()
    {
        foreach (var player in online.All)
        {
            if (proximity.TryLocate(player.Id, out var area, out _) && (!_told.TryGetValue(player, out var told) || told != area))
                Tell(player, area);
        }
    }

    private void OnAsked(PlayerId id)
    {
        if (online.TryGet(id, out var player) && proximity.TryLocate(player.Id, out var area, out _))
            Tell(player, area);
    }

    private void OnAreaChanged(AreaId changed)
    {
        foreach (var (player, told) in _told.ToList())
        {
            if (told == changed)
                Tell(player, told);
        }
    }

    private void Tell(OnlinePlayer player, AreaId area)
    {
        var here = plots.ListIn(area);
        var plotNumbers = here.SelectMany(p => new[]
        {
            p.Box.FirstTileX, p.Box.FirstTileY, p.Box.Width, p.Box.Depth, p.Box.GroundZ, p.Box.Height,
            p.SignPosition.X, p.SignPosition.Y, p.SignPosition.Z, p.SignYaw,
        }).ToArray();
        rpc.SendPlotsHere(player.Id, plots.Settings.SignAsset, here.Select(p => p.Id).ToArray(), plotNumbers);

        var ids = new List<long>();
        var assets = new List<string>();
        var numbers = new List<float>();
        var references = new List<long>();
        foreach (var piece in here.SelectMany(p => plots.ListOn(p.Id)))
        {
            if (!plots.Pieces.TryFind(piece.Piece, out var found))
            {
                // A key the catalogue lost stays in the database, unseen, until the staff put it back or remove the plot.
                logger.LogError("[CyrodiilRP] Piece #{Piece} on plot #{Plot} is '{Key}', which the building catalogue does not list.",
                    piece.Id, piece.PlotId, piece.Piece);
                continue;
            }

            ids.Add(piece.Id);
            assets.Add(found.Asset);
            numbers.AddRange([piece.Position.X, piece.Position.Y, piece.Position.Z, piece.Rotation.X, piece.Rotation.Y, piece.Rotation.Z, found.Scale]);
            references.Add(piece.RefId);
        }

        rpc.SendPiecesHere(player.Id, ids.ToArray(), assets.ToArray(), numbers.ToArray(), references.ToArray());
        _told[player] = area;
    }
}
