using System.Numerics;
using Microsoft.Extensions.Logging;
using OblivionMP.SDK;
using OblivionMP.SDK.Common.Props;
using ReadyM.SDK.Attributes;
using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Client;

/// <summary>
/// The docks on this client: every crate deposit drawn as a pallet of boards (<see cref="PalletLayout"/>) and the
/// server's notes on the panel. With setup allowed it measures the crate and the board for the server; placing is
/// <see cref="PlacementClient"/>'s.
/// </summary>
[Service]
public sealed partial class DocksClient(
    IPlayers players,
    ILocalProps localProps,
    JobsPanel panel,
    DocksClientRpc rpc,
    ILogger logger)
{
    private readonly Dictionary<string, DockZoneView> _zones = [];
    private readonly Dictionary<string, List<LocalPropHandle>> _pallets = [];
    private DocksRules? _rules;
    private Pallet? _pallet;
    private bool _askedForRules;

    /// <summary>What the server last said the docks look like; null between connections.</summary>
    public DocksRules? Rules => _rules;

    private void Start()
    {
        rpc.RulesReceived += OnRules;
        rpc.ZoneAvailable += OnZoneAvailable;
        rpc.ZoneRemoved += OnZoneRemoved;
        rpc.Noted += OnNoted;
    }

    private void Stop()
    {
        rpc.RulesReceived -= OnRules;
        rpc.ZoneAvailable -= OnZoneAvailable;
        rpc.ZoneRemoved -= OnZoneRemoved;
        rpc.Noted -= OnNoted;
        Forget();
    }

    private void Update() => FollowConnection();

    // The rules and deposits come with each connection: what an earlier server sent is dropped
    private void FollowConnection()
    {
        var connected = players.LocalMainCharacter is not null;
        if (connected == _askedForRules)
            return;

        _askedForRules = connected;
        Forget();
        if (connected)
            rpc.SendDocksReady();
    }

    private void Forget()
    {
        foreach (var id in _pallets.Keys.ToList())
            RemovePallet(id);
        _zones.Clear();
        _rules = null;
        _pallet = null;
    }

    private void OnRules(DocksRules rules)
    {
        _rules = rules;
        var board = MeasureBoard(rules);
        _pallet = board is { } measured
            ? PalletLayout.Of(rules.PalletFootprint, measured.Min, measured.Max, rules.PalletWoodShare, rules.PalletRunnerInset)
            : null;
        foreach (var zone in _zones.Values)
            DrawPallet(zone);
        if (!rules.SetupAllowed)
            return;

        // The server packs the inlets' piles by the crate's size and stands them on the pallets, which only a game can measure
        if (localProps.TryMeasure(rules.CrateAsset, rules.CrateScale, out var crate))
            rpc.SendCrateMeasured(crate.Size.X, crate.Size.Y, crate.Size.Z, crate.Min.Z);
        else
            logger.LogWarning("Jobs: could not measure the crate {Asset} for the inlets' piles.", rules.CrateAsset);
        if (board is { } boardBounds)
            rpc.SendBoardMeasured(boardBounds.Min.X, boardBounds.Min.Y, boardBounds.Min.Z, boardBounds.Max.X, boardBounds.Max.Y, boardBounds.Max.Z);
    }

    private PropBounds? MeasureBoard(DocksRules rules)
    {
        if (rules.PalletBoard.Length == 0)
            return null;
        if (localProps.TryMeasure(rules.PalletBoard, 1f, out var bounds))
            return bounds;
        logger.LogWarning("Jobs: could not measure the pallet board {Asset}; the deposits have no pallets.", rules.PalletBoard);
        return null;
    }

    /// <summary>
    /// The boards of a deposit's pallet with its floor (where a crate's origin rests) at <paramref name="floor"/>, turned
    /// by <paramref name="yaw"/>: none before the rules came or without a measured board.
    /// </summary>
    public IReadOnlyList<Prop> PalletProps(Vector3 floor, float yaw)
    {
        if (_rules is not { } rules || _pallet is not { } pallet)
            return [];

        var radians = yaw * MathF.PI / 180f;
        var cos = MathF.Cos(radians);
        var sin = MathF.Sin(radians);
        // The floor is where a crate's origin rests: the ground is the crate's bottom below it
        var ground = floor + new Vector3(0f, 0f, rules.CrateBottom);
        return pallet.Pieces
            .Select(piece => new Prop(rules.PalletBoard,
                ground + new Vector3(piece.Offset.X * cos - piece.Offset.Y * sin, piece.Offset.X * sin + piece.Offset.Y * cos, piece.Offset.Z),
                new Vector3(0, yaw + piece.Yaw, 0), piece.Scale))
            .ToList();
    }

    private void OnZoneAvailable(DockZoneView zone)
    {
        _zones[zone.Id] = zone;
        DrawPallet(zone);
    }

    private void OnZoneRemoved(string id)
    {
        _zones.Remove(id);
        RemovePallet(id);
    }

    private void DrawPallet(DockZoneView zone)
    {
        RemovePallet(zone.Id);
        var boards = PalletProps(zone.Floor, zone.Yaw);
        if (boards.Count > 0)
            _pallets[zone.Id] = boards.Select(board => localProps.Place(board, collides: true)).ToList();
    }

    private void RemovePallet(string id)
    {
        if (!_pallets.Remove(id, out var boards))
            return;
        foreach (var board in boards)
            localProps.Remove(board);
    }

    private void OnNoted(string message, bool refusal) => panel.Say(refusal ? PanelLineKind.Refusal : PanelLineKind.Note, message);
}
