using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Network;
using CyrodiilRP.Server.Players;
using ReadyM.Api.Idents;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Notices;

// Each player's game stands the boards of the area its character is in, and only those: a prop placed for everyone
// (IProps) names no area, and two areas reuse the same coordinates. A player is told the boards as they come into an area,
// again as a board there is stood or taken away, and whenever their game asks.
[Service]
internal sealed partial class BoardProps(RpcHandlers rpc, OnlinePlayers online, Proximity proximity, NoticeBoards boards, Timers timers)
{
    private const string LookJob = "notices.areas";

    // The area whose boards each player's game was last told.
    private readonly Dictionary<OnlinePlayer, AreaId> _told = [];

    private void Start()
    {
        rpc.NoticeBoardsAsked += OnAsked;
        online.Left += player => _told.Remove(player);
        boards.BoardsChanged += OnBoardsChanged;
        timers.RunEvery(LookJob, NoticeRules.LookEvery, Look);
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

    private void OnBoardsChanged(AreaId changed)
    {
        foreach (var (player, told) in _told.ToList())
        {
            if (told == changed)
                Tell(player, told);
        }
    }

    private void Tell(OnlinePlayer player, AreaId area)
    {
        var here = boards.ListBoardsIn(area);
        var spots = here.SelectMany(board => new[] { board.Position.X, board.Position.Y, board.Position.Z, board.Yaw }).ToArray();
        rpc.SendNoticeBoardsHere(player.Id, boards.Settings.Asset, spots);
        _told[player] = area;
    }
}
