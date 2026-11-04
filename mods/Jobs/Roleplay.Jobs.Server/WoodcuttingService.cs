using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using LiteNetLib;
using Microsoft.Extensions.Logging;
using OblivionMp.Sdk.Common.Archetypes;
using OblivionMp.Sdk.Common.Archetypes.Mixins;
using OblivionMp.Sdk.Common.Foliage;
using OblivionMp.Sdk.Serverside.Foliage;
using OblivionMp.Sdk.Serverside.Items;
using OblivionMp.Sdk.Serverside.Props;
using ReadyM.Api.Idents;
using ReadyM.Relay.Common.Oblivion;
using ReadyM.Relay.Common.Oblivion.ECS.Values;
using ReadyM.SDK.Archetypes.Core;
using ReadyM.SDK.Server.Events;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Server.Entities;
using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Server;

/// <summary>
/// The authority on woodcutting. A player asks to chop a tree; this checks they stand at a real tree nobody felled,
/// facing it with an axe in hand, and tells them how to swing. Their client reports each swing that lands on the trunk;
/// at the last hit needed the tree comes down for everyone (<see cref="IFoliage"/>), the logs go into their inventory,
/// and a stump marks the spot (<see cref="IProps"/>) until the tree grows back after
/// <see cref="WoodcuttingConfig.RespawnMinutes"/>.
/// </summary>
[Service]
public sealed partial class WoodcuttingService(
    IEntities entities,
    IServerEvents events,
    IFoliage foliage,
    IProps props,
    WoodcuttingRpcHandlers rpc,
    FelledTreeStore store,
    JobsConfig config,
    ILogger logger)
{
    private readonly record struct Chop(FoliageId Tree, TreeKind Kind, float Scale, int HitsNeeded, double StartedAt, int Hits, double LastHitAt);

    // The client reports its own position and facing a moment late, and stands a little off the trunk
    private const float ReachSlack = 75f;
    private const float FacingSlackDegrees = 15f;
    // The client judges a hit on the same trunk; the slack only covers float rounding on the wire
    private const float HitSlack = 10f;
    // What a client may claim a tree's scale is; vanilla foliage stays well inside
    private const float MinTreeScale = 0.2f;
    private const float MaxTreeScale = 5f;
    // A chop nobody finished or cancelled is dropped this long after its last swing
    private const double AbandonAfterSeconds = 15;

    private readonly Dictionary<PlayerId, Chop> _chops = [];
    // Players whose client asked for the rules, who are told when a tree falls or grows back
    private readonly HashSet<PlayerId> _ready = [];
    private readonly Inventories _inventories = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private WoodcuttingConfig Settings => config.Woodcutting;
    private TreeFilter _filter = new([], []);
    private Dictionary<uint, Axe> _axes = [];
    private List<(FormId Item, TreeDrop Drop)> _drops = [];
    private double _nextRegrowCheck;

    private double Now => _clock.Elapsed.TotalSeconds;

    private void Start()
    {
        _filter = new TreeFilter(Settings.TreeMeshes, Settings.ExcludedMeshes);
        _axes = Settings.Axes.ToDictionary(axe => ParseFormId(axe.Item));
        _drops = [.. Settings.Drops.Select(drop => (new FormId(ParseFormId(drop.Item)), drop))];

        rpc.Ready += OnReady;
        rpc.StartRequested += OnStart;
        rpc.HitReported += OnHit;
        rpc.CancelRequested += OnCancel;
        events.OnPlayerDisconnected += OnDisconnected;

        foreach (var tree in store.Felled.ToList())
        {
            // Felled under an earlier config that counted it a tree: it stands again
            if (!_filter.IsTree(tree.Mesh))
            {
                store.Remove(tree.Id);
                logger.LogInformation("Jobs: {Tree} is no tree under this config, no longer felled.", tree.Id);
                continue;
            }
            foliage.Hide(tree.Id);
            PlaceProps(tree);
        }

        logger.LogInformation(
            "Jobs: woodcutting running. Trees are meshes with {Trees} but not {Excluded}; {Hits} hits for a scale 1 oak, {Drops} drops, {Axes} axes, back after {Minutes} min. {Felled} trees felled.",
            TreeFilter.Join(_filter.TreeMeshes), TreeFilter.Join(_filter.ExcludedMeshes), Settings.BaseHits,
            _drops.Count, _axes.Count, Settings.RespawnMinutes, store.Felled.Count);
    }

    private void Stop()
    {
        rpc.Ready -= OnReady;
        rpc.StartRequested -= OnStart;
        rpc.HitReported -= OnHit;
        rpc.CancelRequested -= OnCancel;
        events.OnPlayerDisconnected -= OnDisconnected;
    }

    private void Update()
    {
        if (Now < _nextRegrowCheck)
            return;
        _nextRegrowCheck = Now + 1;

        var now = DateTime.UtcNow;
        foreach (var tree in store.Felled.Where(tree => tree.GrowsBackUtc <= now).ToList())
        {
            foliage.Show(tree.Id);
            foreach (var player in _ready)
                rpc.SendTreeGrewBack(player, tree.Mesh, tree.X, tree.Y, tree.Z);
            props.Remove(FelledTreeProps.StumpId(tree.Id));
            store.Remove(tree.Id);
            logger.LogInformation("Jobs: {Tree} grew back.", tree.Id);
        }

        foreach (var (player, chop) in _chops.Where(pair => Now > pair.Value.StartedAt + LastsFor(pair.Value)).ToList())
        {
            _chops.Remove(player);
            logger.LogInformation("Jobs: {Player} never finished chopping {Tree}, dropped.", player, chop.Tree);
        }
    }

    private void OnReady(PlayerId player)
    {
        _ready.Add(player);
        rpc.SendWoodcuttingRules(player, TreeFilter.Join(_filter.TreeMeshes), TreeFilter.Join(_filter.ExcludedMeshes),
            Settings.Reach, Settings.FacingDegrees, Settings.TreesFall, Settings.FallSeconds, Settings.FallDegrees,
            Settings.FallLieSeconds, Settings.FallSinkSeconds, Settings.FallSinkDepth, Settings.FallViewDistance);
    }

    private void OnStart(PlayerId player, FoliageId tree, float scale)
    {
        if (_chops.ContainsKey(player))
        {
            Refuse(player, "You are already chopping.");
            return;
        }

        if (Refusal(player, tree, out var character) is { } refusal)
        {
            Refuse(player, refusal);
            return;
        }

        var kind = TreeYield.KindFor(tree.Mesh, Settings);
        var axe = BestAxeEquipped(character);
        var treeScale = Math.Clamp(float.IsFinite(scale) ? scale : 1f, MinTreeScale, MaxTreeScale);
        var hitsNeeded = TreeYield.HitsFor(kind, treeScale, axe?.Tier, Settings);
        _chops[player] = new Chop(tree, kind, treeScale, hitsNeeded, Now, 0, double.NegativeInfinity);
        rpc.SendChopStarted(player, Settings.Animation, Settings.FirstPersonAnimation, Settings.PlayRate, Settings.SwingSeconds,
            hitsNeeded * Settings.SwingsPerHit, hitsNeeded, Settings.HitRadius, kind.Match, kind.Tier, axe?.Tier ?? 0, Settings.WearPerHit);
        rpc.SendChopProgress(player, 0, hitsNeeded);
        logger.LogInformation("Jobs: {Player} started chopping {Tree}, scale {Scale:0.00}, kind {Kind} (tier {TreeTier}) with {Axe}: {Hits} hits.",
            player, tree, treeScale, KindName(kind), kind.Tier, axe is null ? "no axe needed" : $"{axe.Name} (tier {axe.Tier})", hitsNeeded);
    }

    private void OnHit(PlayerId player, Vector3 at)
    {
        if (!_chops.TryGetValue(player, out var chop))
        {
            logger.LogDebug("Jobs: {Player} reported a hit at {At} with no chop going, ignored.", player, at);
            return;
        }

        if (!ChopGeometry.IsOnTrunk(at, chop.Tree.Position, Settings.HitRadius + HitSlack))
        {
            logger.LogWarning("Jobs: {Player} reported a hit at {At}, which is off the trunk of {Tree}. Ignored.", player, at, chop.Tree);
            return;
        }

        if (Now - chop.LastHitAt < Settings.MinSecondsBetweenHits)
        {
            logger.LogWarning("Jobs: {Player} reported hits on {Tree} {Seconds:0.00} s apart. Ignored.", player, chop.Tree, Now - chop.LastHitAt);
            return;
        }

        if (Refusal(player, chop.Tree, out var character) is { } refusal)
        {
            _chops.Remove(player);
            Refuse(player, refusal);
            return;
        }

        var hits = chop.Hits + 1;
        if (hits < chop.HitsNeeded)
        {
            _chops[player] = chop with { Hits = hits, LastHitAt = Now };
            rpc.SendChopProgress(player, hits, chop.HitsNeeded);
            return;
        }

        _chops.Remove(player);
        var felled = new FelledTree(chop.Tree.Mesh, chop.Tree.X, chop.Tree.Y, chop.Tree.Z,
            DateTime.UtcNow.AddMinutes(Settings.RespawnMinutes), chop.Scale, FelledTreeProps.FallYaw(character.Position, chop.Tree.Position));
        foliage.Hide(chop.Tree);
        PlaceProps(felled);
        store.Add(felled);
        foreach (var ready in _ready)
            rpc.SendTreeFelled(ready, chop.Tree.Mesh, chop.Tree.X, chop.Tree.Y, chop.Tree.Z, chop.Kind.Match, chop.Scale, ready == player,
                felled.FallYaw);
        var given = new List<string>();
        foreach (var (item, drop) in _drops)
        {
            var count = TreeYield.CountFor(drop, chop.Tree.Mesh, chop.Kind, chop.Scale, Random.Shared.NextDouble);
            if (count <= 0)
                continue;
            _inventories.Add(character.As<Equipment>(), new InventoryEntry(item, count, 0));
            given.Add($"{count} {drop.Name}");
        }

        var what = given.Count > 0 ? $"You get {string.Join(", ", given)}." : "The wood is rotten through: nothing worth taking.";
        rpc.SendChopEnded(player, (byte)ChopOutcome.Felled, $"Timber! {what}");
        logger.LogInformation("Jobs: {Player} felled {Tree} in {Seconds:0.0} s for {Given}.", player, chop.Tree, Now - chop.StartedAt,
            given.Count > 0 ? string.Join(", ", given) : "nothing");
    }

    private void PlaceProps(FelledTree tree)
    {
        props.Place(FelledTreeProps.StumpId(tree.Id), FelledTreeProps.Stump(tree.Id, tree.TreeScale, Settings));
    }

    private void OnCancel(PlayerId player)
    {
        if (_chops.Remove(player, out var chop))
            logger.LogInformation("Jobs: {Player} stopped chopping {Tree}.", player, chop.Tree);
    }

    private void OnDisconnected(Player scope, Guid _, DisconnectReason reason)
    {
        PlayerId player = scope.PlayerId;
        _chops.Remove(player);
        _ready.Remove(player);
    }

    // Why this player cannot chop this tree now, or null when they can
    private string? Refusal(PlayerId player, FoliageId tree, out MainCharacter character)
    {
        if (!entities.TryLookup<MainCharacter, PlayerId>(player, out character))
            return "Your character is not in the world yet.";
        if (character.IsDead)
            return "You cannot chop wood while dead.";
        if (!_filter.IsTree(tree.Mesh))
        {
            logger.LogWarning("Jobs: {Player} asked to chop {Mesh}, which is no tree here.", player, tree.Mesh);
            return "That is not a tree you can fell.";
        }
        if (foliage.IsHidden(tree))
            return "Someone already felled that tree.";
        if (_chops.Any(pair => pair.Key != player && pair.Value.Tree == tree))
            return "Someone else is chopping that tree.";
        if (ChopGeometry.HorizontalDistance(character.Position, tree.Position) > Settings.Reach + ReachSlack)
            return "You are too far from the tree.";
        if (ChopGeometry.DegreesOffFacing(character.Position, character.Rotation, tree.Position) > Settings.FacingDegrees + FacingSlackDegrees)
            return "Face the tree to chop it.";
        if (_axes.Count > 0 && BestAxeEquipped(character) is null)
            return "You need a battle axe in your hands.";
        return null;
    }

    // The highest tier axe the character has equipped, or null for none (and always when no axe is needed)
    private Axe? BestAxeEquipped(MainCharacter character)
    {
        Axe? best = null;
        for (var i = 0; i < character.ItemsCount; i++)
        {
            var entry = character.GetItems(i);
            if (entry.Worn != 0 && _axes.TryGetValue(entry.FormId.FormId.Value, out var axe) && (best is null || axe.Tier > best.Tier))
                best = axe;
        }
        return best;
    }

    // Every swing the client may take on it, then the grace for a client that went quiet
    private double LastsFor(Chop chop) => chop.HitsNeeded * Settings.SwingsPerHit * Settings.SwingSeconds + AbandonAfterSeconds;

    private static string KindName(TreeKind kind) => kind.Match.Length > 0 ? kind.Match : "default";

    private void Refuse(PlayerId player, string message) => rpc.SendChopEnded(player, (byte)ChopOutcome.Refused, message);

    private static uint ParseFormId(string text)
    {
        var digits = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? text[2..] : text;
        if (!uint.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            throw new FormatException($"Jobs config: '{text}' is not a form id. Write it in hex, such as 0x00000D7F.");
        return value;
    }
}
