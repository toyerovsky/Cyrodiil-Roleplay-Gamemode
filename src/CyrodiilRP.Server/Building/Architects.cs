using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text.Json;
using CyrodiilRP.Common.Building;
using CyrodiilRP.Data;
using CyrodiilRP.Data.Staff;
using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Network;
using CyrodiilRP.Server.Players;
using CyrodiilRP.Server.Staff;
using Microsoft.Extensions.Logging;
using ReadyM.Api.Idents;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Building;

// Architect mode: the staff choose a plot or mark a new one, and place, move and remove pieces on it, from the architect
// page over the game. Every change the game sends is checked here as the page checked it, and written before it is
// shown. The mode closes off duty, out of the world, a walk away from the plot, or with the plot.
[Service]
internal sealed partial class Architects(
    RpcHandlers rpc,
    OnlinePlayers online,
    Proximity proximity,
    Plots plots,
    BuildingReferences references,
    RpChat chat,
    StaffRoles roles,
    Audit audit,
    Timers timers,
    Fire fire,
    ILogger logger)
{
    private const string WatchJob = "building.architects";
    private const string MarkAction = "plot.mark";
    private const string PlaceAction = "building.place";
    private const string MoveAction = "building.move";
    private const string RemoveAction = "building.remove";
    private const float CentimetresPerMetre = 100f;

    private readonly Dictionary<OnlinePlayer, ArchitectSession> _open = [];

    public bool IsOpen(OnlinePlayer player) => _open.ContainsKey(player);

    public void OpenChoosing(OnlinePlayer player) => Open(player, new ArchitectSession(ArchitectTool.Choose, 0));

    public void OpenMarking(OnlinePlayer player) => Open(player, new ArchitectSession(ArchitectTool.Mark, 0));

    public void OpenBuilding(OnlinePlayer player, PlotRecord plot) => Open(player, new ArchitectSession(ArchitectTool.Build, plot.Id));

    private void Open(OnlinePlayer player, ArchitectSession session)
    {
        _open[player] = session;
        var pieces = plots.Pieces.All;
        rpc.SendArchitectCatalogue(player.Id, pieces.Select(p => p.Key).ToArray(), pieces.Select(p => p.Asset).ToArray(),
            pieces.Select(p => p.Scale).ToArray());
        SendState(player);
    }

    private void Start()
    {
        rpc.ArchitectPlaced += OnPlaced;
        rpc.ArchitectMoved += OnMoved;
        rpc.ArchitectRemoved += OnRemoved;
        rpc.ArchitectMeasured += OnMeasured;
        rpc.ArchitectMarked += OnMarked;
        rpc.ArchitectLeft += OnLeft;
        rpc.ArchitectChose += OnChose;
        rpc.ArchitectNewPlotAsked += OnNewPlotAsked;
        online.Left += player => _open.Remove(player);
        plots.PlotRemoved += OnPlotRemoved;
        plots.AreaChanged += OnAreaChanged;
        timers.RunEvery(WatchJob, BuildingRules.LookEvery, Watch);
    }

    private void OnPlaced(PlayerId id, string key, float[] spot)
    {
        if (!TryFindBuilding(id, out var player, out var plot))
            return;

        if (!ArchitectRules.TryReadSpot(spot, out var position, out var yaw))
        {
            logger.LogWarning("[CyrodiilRP] {Player} sent a spot of {Count} numbers to place '{Piece}'.", player.Ref, spot.Length, key);
            Answer(player, BuildingText.Malformed, done: false);
            return;
        }

        if (!plots.Pieces.TryFind(key, out var piece))
        {
            Answer(player, BuildingText.UnknownPiece, done: false);
            return;
        }

        if (Refuse(plot, piece, position, yaw) is { } refusal)
        {
            Answer(player, refusal, done: false);
            return;
        }

        // The game's own object is made first, so the row can name it, and taken away again if the row is not written
        var refId = piece.Object == PieceObject.Mesh ? 0u : references.Create(piece, player, position, yaw);
        if (piece.Object != PieceObject.Mesh && refId == 0)
        {
            Answer(player, BuildingText.NoCell, done: false);
            return;
        }

        fire.Run(PlaceAction, async () =>
        {
            PlotObjectRecord placed;
            try
            {
                placed = await plots.AddObjectAsync(plot, piece, position, yaw, PlotObjectState.Built, refId, player.Ref);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "[CyrodiilRP] {Player} could not place '{Piece}' on plot #{Plot}.", player.Ref, piece.Key, plot.Id);
                if (refId != 0)
                    references.Delete(refId);
                Answer(player, BuildingText.ChangeFailed, done: false);
                return;
            }

            audit.Log(PlaceAction, player.Ref, null, new PieceDetails(placed.Id, plot.Id, piece.Key, position.X, position.Y, position.Z, yaw));
            Answer(player, BuildingText.Placed(piece.Name), done: true);
            SendState(player);
        });
    }

    private void OnMoved(PlayerId id, long pieceId, float[] spot)
    {
        if (!TryFindBuilding(id, out var player, out var plot))
            return;

        if (!ArchitectRules.TryReadSpot(spot, out var position, out var yaw))
        {
            logger.LogWarning("[CyrodiilRP] {Player} sent a spot of {Count} numbers to move piece #{Piece}.", player.Ref, spot.Length, pieceId);
            Answer(player, BuildingText.Malformed, done: false);
            return;
        }

        if (!TryFindOnPlot(pieceId, plot, out var placed, out var piece))
        {
            Answer(player, BuildingText.Gone, done: false);
            return;
        }

        if (Refuse(plot, piece, position, yaw) is { } refusal)
        {
            Answer(player, refusal, done: false);
            return;
        }

        fire.Run(MoveAction, async () =>
        {
            PlotObjectRecord? moved;
            try
            {
                moved = await plots.MoveObjectAsync(plot, placed, position, yaw);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "[CyrodiilRP] {Player} could not move piece #{Piece} on plot #{Plot}.", player.Ref, placed.Id, plot.Id);
                Answer(player, BuildingText.ChangeFailed, done: false);
                return;
            }

            if (moved is null)
            {
                Answer(player, BuildingText.Gone, done: false);
                return;
            }

            if (moved.RefId != 0 && !references.Move(moved.RefId, position, yaw))
                logger.LogWarning("[CyrodiilRP] Piece #{Piece} moved, but its game object {RefId:X} is not in the world to move.", moved.Id, moved.RefId);

            audit.Log(MoveAction, player.Ref, null, new PieceDetails(placed.Id, plot.Id, piece.Key, position.X, position.Y, position.Z, yaw));
            Answer(player, BuildingText.Moved(piece.Name), done: true);
        });
    }

    private void OnRemoved(PlayerId id, long pieceId)
    {
        if (!TryFindBuilding(id, out var player, out var plot))
            return;

        if (!TryFindOnPlot(pieceId, plot, out var placed, out var piece))
        {
            Answer(player, BuildingText.Gone, done: false);
            return;
        }

        fire.Run(RemoveAction, async () =>
        {
            bool removed;
            try
            {
                removed = await plots.RemoveObjectAsync(plot, placed);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "[CyrodiilRP] {Player} could not remove piece #{Piece} from plot #{Plot}.", player.Ref, placed.Id, plot.Id);
                Answer(player, BuildingText.ChangeFailed, done: false);
                return;
            }

            if (!removed)
            {
                Answer(player, BuildingText.Gone, done: false);
                return;
            }

            if (placed.RefId != 0 && !references.Delete(placed.RefId))
                logger.LogWarning("[CyrodiilRP] Piece #{Piece} is gone, but its game object {RefId:X} was not in the world to take away.", placed.Id, placed.RefId);

            audit.Log(RemoveAction, player.Ref, null,
                new PieceDetails(placed.Id, plot.Id, piece.Key, placed.Position.X, placed.Position.Y, placed.Position.Z, placed.Rotation.Y));
            Answer(player, BuildingText.Removed(piece.Name), done: true);
            SendState(player);
        });
    }

    // Only an architect's game measures, and only the first measurement of a piece stands.
    private void OnMeasured(PlayerId id, string key, float[] bounds)
    {
        if (!online.TryGet(id, out var player) || !_open.ContainsKey(player))
        {
            logger.LogWarning("[CyrodiilRP] Player {Player} measured '{Piece}' outside architect mode.", id.RawValue, key);
            return;
        }

        if (!ArchitectRules.TryReadBounds(bounds, out var min, out var max) || !plots.Pieces.Measure(key, min, max))
            logger.LogWarning("[CyrodiilRP] {Player} sent a box of {Count} numbers for '{Piece}', which is no piece's box.", player.Ref, bounds.Length, key);
    }

    private void OnMarked(PlayerId id, PlotMarking marking)
    {
        if (!online.TryGet(id, out var player) || !_open.TryGetValue(player, out var session) || session.Tool != ArchitectTool.Mark)
            return;

        if (ReadForm(player, marking.Form) is not { } form)
        {
            Answer(player, BuildingText.Malformed, done: false);
            return;
        }

        var plan = ArchitectRules.Plan(marking, form, plots.Settings);
        if (!proximity.TryLocate(player.Id, out var area, out _))
        {
            Close(player, BuildingText.ClosedNotInWorld);
            return;
        }

        var verdict = plan.Verdict == MarkingVerdict.Accepted && plots.Overlaps(area, plan.Box) ? MarkingVerdict.Overlaps : plan.Verdict;
        if (verdict != MarkingVerdict.Accepted)
        {
            if (verdict == MarkingVerdict.Malformed)
                logger.LogWarning("[CyrodiilRP] {Player} sent a plot of {Tiles} tiles and {Ground} ground numbers.", player.Ref, marking.Tiles.Length, marking.Ground.Length);

            Answer(player, Explain(verdict), done: false);
            return;
        }

        fire.Run(MarkAction, async () =>
        {
            PlotRecord plot;
            try
            {
                plot = await plots.AddPlotAsync(area.ToManaged(), plan.Box, form.Kind, form.Price, form.WeeklyTax, plan.Sign, plan.SignYaw, player.Ref);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "[CyrodiilRP] {Player} could not mark a plot.", player.Ref);
                Answer(player, BuildingText.PlotFailed, done: false);
                return;
            }

            audit.Log(MarkAction, player.Ref, null, new PlotDetails(plot.Id, plot.Area, plot.Box.FirstTileX, plot.Box.FirstTileY, plot.Box.Width,
                plot.Box.Depth, plot.Kind, plot.Price, plot.WeeklyTax));
            Answer(player, BuildingText.PlotMade(plot.Id, plot.Box.Width, plot.Box.Depth), done: true);
            if (player.IsLive && _open.ContainsKey(player))
                OpenBuilding(player, plot);
        });
    }

    private Phrase Explain(MarkingVerdict verdict) => verdict switch
    {
        MarkingVerdict.Accepted => throw new ArgumentOutOfRangeException(nameof(verdict), verdict, "An accepted plot needs no explaining."),
        MarkingVerdict.Malformed => BuildingText.Malformed,
        MarkingVerdict.TooBig => BuildingText.TooBig(plots.Settings.MostTiles),
        MarkingVerdict.FormInvalid => BuildingText.FormInvalid,
        MarkingVerdict.SignFar => BuildingText.SignFar,
        MarkingVerdict.Overlaps => BuildingText.Overlaps,
    };

    // Null for what is no form: a page's garbage.
    private PlotForm? ReadForm(OnlinePlayer player, string json)
    {
        try
        {
            return JsonSerializer.Deserialize(json, ArchitectJson.Default.PlotForm);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "[CyrodiilRP] {Player} sent a plot form that is no form.", player.Ref);
            return null;
        }
    }

    // A plot from the list or the world (its id), or 0: back to choosing one.
    private void OnChose(PlayerId id, long plotId)
    {
        if (!online.TryGet(id, out var player) || !_open.ContainsKey(player))
            return;

        if (!roles.Allows(player, StaffPermissions.Build))
        {
            Answer(player, BuildingText.NotYours, done: false);
            return;
        }

        if (plotId == 0)
        {
            _open[player] = new ArchitectSession(ArchitectTool.Choose, 0);
            SendState(player);
            return;
        }

        if (!plots.TryFindPlot(plotId, out var plot))
        {
            Answer(player, BuildingText.PlotGone(plotId), done: false);
            return;
        }

        if (!proximity.TryLocate(player.Id, out var area, out var position) || area != plot.AreaId
            || DistanceTo(plot, position) > BuildingRules.WalkAway)
        {
            Answer(player, BuildingText.PlotFar(plotId, BuildingRules.WalkAwayMetres), done: false);
            return;
        }

        _open[player] = new ArchitectSession(ArchitectTool.Build, plot.Id);
        SendState(player);
    }

    private void OnNewPlotAsked(PlayerId id)
    {
        if (!online.TryGet(id, out var player) || !_open.ContainsKey(player))
            return;

        if (!roles.Allows(player, StaffPermissions.Plot))
        {
            Answer(player, BuildingText.NotYours, done: false);
            return;
        }

        _open[player] = new ArchitectSession(ArchitectTool.Mark, 0);
        SendState(player);
    }

    // The list of plots to choose from follows the plots of the area.
    private void OnAreaChanged(AreaId area)
    {
        foreach (var (player, session) in _open.ToList())
        {
            if (session.Tool == ArchitectTool.Choose && proximity.TryLocate(player.Id, out var at, out _) && at == area)
                SendState(player);
        }
    }

    private static float DistanceTo(PlotRecord plot, Vector3 position) =>
        new Vector2(position.X - Math.Clamp(position.X, plot.Box.MinX, plot.Box.MaxX),
            position.Y - Math.Clamp(position.Y, plot.Box.MinY, plot.Box.MaxY)).Length();

    private void OnLeft(PlayerId id)
    {
        if (online.TryGet(id, out var player))
            _open.Remove(player);
    }

    private void OnPlotRemoved(PlotRecord plot)
    {
        foreach (var (player, session) in _open.ToList())
        {
            if (session.Tool == ArchitectTool.Build && session.PlotId == plot.Id)
                Close(player, BuildingText.ClosedGone);
        }
    }

    private void Watch()
    {
        if (_open.Count == 0)
            return;

        foreach (var (player, session) in _open.ToList())
        {
            if (CheckSession(player, session) is { } broken)
                Close(player, broken);
        }
    }

    private Phrase? CheckSession(OnlinePlayer player, ArchitectSession session)
    {
        var allowed = session.Tool switch
        {
            ArchitectTool.Choose => StaffPermissions.Build,
            ArchitectTool.Mark => StaffPermissions.Plot,
            ArchitectTool.Build => StaffPermissions.Build,
        };
        if (player.DutySince is null || !roles.Allows(player, allowed))
            return BuildingText.ClosedOffDuty;

        if (!proximity.TryLocate(player.Id, out var area, out var position))
            return BuildingText.ClosedNotInWorld;

        if (session.Tool is ArchitectTool.Choose or ArchitectTool.Mark)
            return null;

        if (!plots.TryFindPlot(session.PlotId, out var plot))
            return BuildingText.ClosedGone;

        return area == plot.AreaId && DistanceTo(plot, position) <= BuildingRules.WalkAway ? null : BuildingText.ClosedFar(BuildingRules.WalkAwayMetres);
    }

    // False once the mode closed, was never opened or marks a plot. One that no longer holds is closed here.
    private bool TryFindBuilding(PlayerId id, [NotNullWhen(true)] out OnlinePlayer? player, [NotNullWhen(true)] out PlotRecord? plot)
    {
        plot = null;
        if (!online.TryGet(id, out player) || !_open.TryGetValue(player, out var session) || session.Tool != ArchitectTool.Build)
            return false;

        if (CheckSession(player, session) is { } broken)
        {
            Close(player, broken);
            return false;
        }

        return plots.TryFindPlot(session.PlotId, out plot);
    }

    private bool TryFindOnPlot(long pieceId, PlotRecord plot, [NotNullWhen(true)] out PlotObjectRecord? placed, [NotNullWhen(true)] out Piece? piece)
    {
        piece = null;
        return plots.TryFindObject(pieceId, out placed) && placed.PlotId == plot.Id && plots.Pieces.TryFind(placed.Piece, out piece);
    }

    private static Phrase? Refuse(PlotRecord plot, Piece piece, Vector3 position, float yaw) => ArchitectRules.Judge(plot.Box, piece, position, yaw) switch
    {
        PlotFit.Fits => null,
        PlotFit.Outside => BuildingText.Outside,
        PlotFit.TooHigh => BuildingText.TooHigh((int)MathF.Round(plot.Box.Height / CentimetresPerMetre)),
    };

    private void Close(OnlinePlayer player, Phrase why)
    {
        _open.Remove(player);
        if (!player.IsLive)
            return;

        rpc.SendArchitectClosed(player.Id);
        chat.Notify(player, why);
    }

    // Onto the page, or into the corner when the mode closed while the change was being written.
    private void Answer(OnlinePlayer player, Phrase text, bool done)
    {
        if (!player.IsLive)
            return;

        if (_open.ContainsKey(player))
            rpc.SendArchitectAnswer(player.Id, chat.Translate(player, text), done);
        else
            chat.Notify(player, text);
    }

    private void SendState(OnlinePlayer player)
    {
        if (!player.IsLive || !_open.TryGetValue(player, out var session))
            return;

        var settings = plots.Settings;
        ArchitectPlotView? plotView = null;
        if (session.Tool == ArchitectTool.Build && plots.TryFindPlot(session.PlotId, out var plot))
            plotView = new ArchitectPlotView(plot.Id, plot.Kind, plot.Box.Width, plot.Box.Depth, (int)MathF.Round(plot.Box.Height / CentimetresPerMetre),
                plot.Price, plot.WeeklyTax, plots.CountOn(plot.Id));

        var choices = session.Tool == ArchitectTool.Choose ? ListChoices(player) : [];
        var view = new ArchitectView(
            session.Tool,
            plotView,
            choices,
            plots.Pieces.All.Select(p => new ArchitectPieceView(p.Key, p.Name, p.Category, p.Use)).ToList(),
            Enum.GetValues<PieceCategory>(),
            new ArchitectFormView(PlotKind.Residential, settings.PlotPrice, settings.PlotWeeklyTax, (int)MathF.Round(settings.PlotHeight / CentimetresPerMetre),
                settings.MostTiles, ArchitectRules.MostGold, ArchitectRules.MostHeightMetres, BuildingGrid.TileCentimetres));
        rpc.SendArchitectState(player.Id, JsonSerializer.Serialize(view, ArchitectJson.Default.ArchitectView), session.PlotId, session.Tool.ToString());
    }

    // The plots of the architect's area within reach, the nearest first.
    private List<ArchitectPlotChoice> ListChoices(OnlinePlayer player)
    {
        if (!proximity.TryLocate(player.Id, out var area, out var position))
            return [];

        return plots.ListIn(area)
            .Where(plot => DistanceTo(plot, position) <= BuildingRules.WalkAway)
            .OrderBy(plot => DistanceTo(plot, position))
            .Select(plot => new ArchitectPlotChoice(plot.Id, plot.Kind, plot.Box.Width, plot.Box.Depth, plots.CountOn(plot.Id),
                (int)MathF.Round(DistanceTo(plot, position) / CentimetresPerMetre)))
            .ToList();
    }

    private sealed record PieceDetails(long Piece, long Plot, string Key, float X, float Y, float Z, float Yaw);

    private sealed record PlotDetails(long Plot, string Area, int FirstTileX, int FirstTileY, int Width, int Depth, PlotKind Kind, int Price, int WeeklyTax);
}
