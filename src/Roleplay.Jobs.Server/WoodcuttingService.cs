using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using LiteNetLib;
using Microsoft.Extensions.Logging;
using OblivionMP.SDK.Common.Archetypes;
using OblivionMP.SDK.Common.Archetypes.Mixins;
using OblivionMP.SDK.Common.Foliage;
using OblivionMP.SDK.Serverside.Foliage;
using OblivionMP.SDK.Serverside.Items;
using OblivionMP.SDK.Serverside.Props;
using ReadyM.Api.Idents;
using ReadyM.Relay.Common.Oblivion;
using ReadyM.Relay.Common.Oblivion.ECS.Values;
using ReadyM.SDK.Archetypes.Core;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Events;
using ReadyM.SDK.Server.Entities;
using ReadyM.SDK.Server.Events;
using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Server;

/// <summary>
/// The authority on woodcutting, which is hitting, as mining is: a client reports each battle axe power attack that
/// lands on a tree, and this checks the player stands there with an axe in hand (and, with
/// <see cref="WoodcuttingConfig.LumberCampsOnly"/>, that the tree is in a lumber camp) and counts the hit. A tree comes
/// down at the hits it needs, from anyone, through <see cref="TreeFelledEvent"/>: it goes for everyone
/// (<see cref="IFoliage"/>), a stump marks the spot (<see cref="IProps"/>) until it grows back after
/// <see cref="WoodcuttingConfig.RespawnMinutes"/>, and what it gives is shared by those hits, the best of it to whoever
/// struck most (<see cref="YieldShare.Share"/>).
/// </summary>
[Service]
public sealed partial class WoodcuttingService(
    IEntities entities,
    IServerEvents serverEvents,
    INetworkedEvents events,
    IFoliage foliage,
    IProps props,
    WoodcuttingRpcHandlers rpc,
    FelledTreeStore store,
    JobAreas areas,
    JobsConfig config,
    ILogger logger)
{
    // The client reports a hit a moment after it landed, from where it stood then
    private const float ReachSlack = 100f;
    // The client names the tree its axe struck; the slack only covers float rounding on the wire
    private const float HitSlack = 10f;
    // What a client may claim a tree's scale is; vanilla foliage stays well inside
    private const float MinTreeScale = 0.2f;
    private const float MaxTreeScale = 5f;
    // A refusal is said at most this often to one player: every power attack on a tree outside a camp would repeat it
    private const double RefusalEverySeconds = 8;

    // Players whose client asked for the rules, who are told when a tree grows back
    private readonly HashSet<PlayerId> _ready = [];
    private readonly Dictionary<FoliageId, HitTally<PlayerId>> _tallies = [];
    private readonly Dictionary<PlayerId, double> _lastHitAt = [];
    private readonly Dictionary<PlayerId, double> _lastRefusedAt = [];
    private readonly Inventories _inventories = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private WoodcuttingConfig Settings => config.Woodcutting;
    private TreeFilter _filter = new([], []);
    private Dictionary<uint, Axe> _axes = [];
    private Dictionary<TreeDrop, FormId> _dropItems = [];
    private double _nextRegrowCheck;

    private double Now => _clock.Elapsed.TotalSeconds;

    private void Start()
    {
        _filter = new TreeFilter(Settings.TreeMeshes, Settings.ExcludedMeshes);
        _axes = Settings.Axes.ToDictionary(axe => ParseFormId(axe.Item));
        _dropItems = Settings.Drops.ToDictionary(drop => drop, drop => new FormId(ParseFormId(drop.Item)));

        rpc.Ready += OnReady;
        rpc.HitReported += OnHit;
        serverEvents.OnPlayerDisconnected += OnDisconnected;

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
            "Jobs: woodcutting by damage, {Normal} a normal attack and {Power} a power attack. Trees are meshes with {Trees} but not {Excluded}; {Health} health for a scale 1 oak, {Drops} drops, {Axes} axes, back after {Minutes} min; {Where}. {Felled} trees felled.",
            Settings.NormalDamage, Settings.PowerDamage, TreeFilter.Join(_filter.TreeMeshes), TreeFilter.Join(_filter.ExcludedMeshes), Settings.BaseHealth,
            _dropItems.Count, _axes.Count, Settings.RespawnMinutes,
            Settings.LumberCampsOnly ? $"only in {areas.LumberCamps.Count} lumber camps" : "anywhere", store.Felled.Count);
    }

    private void Stop()
    {
        rpc.Ready -= OnReady;
        rpc.HitReported -= OnHit;
        serverEvents.OnPlayerDisconnected -= OnDisconnected;
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

        foreach (var (tree, _) in _tallies.Where(pair => Now - pair.Value.LastHitAt > Settings.TreeMemorySeconds).ToList())
            _tallies.Remove(tree);
    }

    private void OnReady(PlayerId player)
    {
        _ready.Add(player);
        rpc.SendWoodcuttingRules(player, TreeFilter.Join(_filter.TreeMeshes), TreeFilter.Join(_filter.ExcludedMeshes),
            Settings.HitRadius, Settings.LumberCampsOnly, Settings.TreesFall,
            Settings.FallSeconds, Settings.FallDegrees, Settings.FallLieSeconds, Settings.FallSinkSeconds, Settings.FallSinkDepth,
            Settings.FallViewDistance, Settings.ChipsPerHit, string.Join('|', Settings.ChipAssets), Settings.ChipSpeed,
            Settings.ChipSeconds, Settings.ChipScale);
        if (!Settings.LumberCampsOnly)
            return;
        foreach (var camp in areas.LumberCamps)
            rpc.SendLumberCampAvailable(player, camp.Name, camp.Center.X, camp.Center.Y, camp.Center.Z, camp.Radius);
    }

    private void OnHit(PlayerId player, FoliageId tree, float scale, Vector3 at, byte attackCode)
    {
        if (!Enum.IsDefined((JobAttack)attackCode))
        {
            logger.LogWarning("Jobs: {Player} reported a hit by attack {Attack}, which is none. Ignored.", player, attackCode);
            return;
        }
        var attack = (JobAttack)attackCode;
        if (!_filter.IsTree(tree.Mesh))
        {
            logger.LogWarning("Jobs: {Player} reported a hit on {Mesh}, which is no tree here. Ignored.", player, tree.Mesh);
            return;
        }
        if (foliage.IsHidden(tree))
        {
            logger.LogDebug("Jobs: {Player} hit {Tree}, which is already down. Ignored.", player, tree);
            return;
        }

        var treeScale = Math.Clamp(float.IsFinite(scale) ? scale : 1f, MinTreeScale, MaxTreeScale);
        if (!ChopGeometry.IsOnTrunk(at, tree.Position, Settings.HitRadius * MathF.Max(1f, treeScale) + HitSlack))
        {
            logger.LogWarning("Jobs: {Player} reported a hit at {At}, which is off the trunk of {Tree}. Ignored.", player, at, tree);
            return;
        }
        if (TooSoon(player, attack))
            return;
        if (Refusal(player, tree, at, out var character, out var axe, out var condition) is { } refusal)
        {
            Refuse(player, refusal);
            return;
        }
        if (axe is not null && Dull(player, condition))
            return;

        var kind = TreeYield.KindFor(tree.Mesh, Settings);
        var health = TreeYield.HealthFor(kind, treeScale, axe?.Tier, Settings);
        var damage = HitDamage.DamageOf(attack, HitDamage.StrengthOf(character), HitDamage.TiersAbove(axe?.Tier, kind.Tier), Settings);
        if (!_tallies.TryGetValue(tree, out var tally))
            _tallies[tree] = tally = new HitTally<PlayerId>();
        tally.Add(player, Now, damage);
        _lastHitAt[player] = Now;
        if (Settings.ChipsPerHit > 0)
        {
            foreach (var ready in _ready)
                rpc.SendTreeStruck(ready, at.X, at.Y, at.Z, character.Position.X, character.Position.Y, character.Position.Z);
        }
        rpc.SendChopProgress(player, kind.Match, Math.Min(tally.Hits, health), health, _axes.Count > 0 ? Settings.WearPerDamage * damage : 0f);
        if (tally.Hits < health)
            return;

        if (!character.TryAs(out Character feller))
            return;
        events.RaiseAs(player, new TreeFelledEvent
        {
            Feller = feller, Mesh = tree.Mesh, X = tree.X, Y = tree.Y, Z = tree.Z, Kind = kind.Match, Scale = treeScale,
            FallYaw = FelledTreeProps.FallYaw(character.Position, tree.Position),
        });
    }

    /// <summary>A tree comes down only while it stands: two last hits at once fell it once.</summary>
    [EventHandler(Stage.Validate)]
    public void CheckFelling(in TreeFelledEvent felled, EventContext context)
    {
        var tree = felled.Tree();
        if (felled.Mesh is null || !_filter.IsTree(tree.Mesh))
            context.Cancel(HarvestRefusal.Other);
        else if (foliage.IsHidden(tree))
            context.Cancel(HarvestRefusal.Gone);
    }

    /// <summary>A felling that passed every check, this mod's and any other's: the tree goes, a stump stays, and the
    /// wood is shared among everyone who hit it.</summary>
    [EventHandler(Stage.Apply)]
    public void Fell(in TreeFelledEvent felled, EventContext context)
    {
        var tree = felled.Tree();
        var feller = JobEvents.PlayerOf(felled.Feller);
        var scale = Math.Clamp(float.IsFinite(felled.Scale) && felled.Scale > 0f ? felled.Scale : 1f, MinTreeScale, MaxTreeScale);
        var fallYaw = felled.FallYaw;
        Guard("felling", feller, () => Fell(tree, feller, scale, fallYaw));
    }

    /// <summary>Tells the feller why the tree stays up: someone else felled it a moment before, or another mod
    /// refused.</summary>
    [EventHandler(Stage.Rejected)]
    public void FellingRefused(in TreeFelledEvent felled, HarvestRefusal reason, EventContext context)
    {
        var tree = felled.Tree();
        var feller = JobEvents.PlayerOf(felled.Feller);
        if (reason == HarvestRefusal.Gone)
            _tallies.Remove(tree);
        logger.LogInformation("Jobs: felling {Tree} by {Player} refused: {Reason}.", tree, feller, reason);
        rpc.SendChopEnded(feller, (byte)ChopOutcome.Refused, JobEvents.Describe(reason), "");
    }

    private void Fell(FoliageId tree, PlayerId feller, float scale, float fallYaw)
    {
        var tally = _tallies.Remove(tree, out var hits) ? hits : null;
        if (tally is null)
        {
            // Raised by another mod with no hits behind it: the feller gets it all
            tally = new HitTally<PlayerId>();
            tally.Add(feller, Now);
        }

        var felled = new FelledTree(tree.Mesh, tree.X, tree.Y, tree.Z, DateTime.UtcNow.AddMinutes(Settings.RespawnMinutes), scale, fallYaw);
        foliage.Hide(tree);
        PlaceProps(felled);
        store.Add(felled);
        ShareYield(tree, scale, tally, feller);
        logger.LogInformation("Jobs: {Player} felled {Tree}, scale {Scale:0.00}, after {Hits}.", feller, tree, scale, tally);
    }

    // The tree's yield, rolled once, shared by everyone who struck it and is still in the world
    private void ShareYield(FoliageId tree, float scale, HitTally<PlayerId> tally, PlayerId feller)
    {
        var choppers = tally.ByPlayer
            .Select(entry => (entry.Player, entry.Hits, Found: entities.TryLookup<MainCharacter, PlayerId>(entry.Player, out var character), Character: character))
            .Where(entry => entry.Found)
            .ToList();
        var kind = TreeYield.KindFor(tree.Mesh, Settings);
        var items = Settings.Drops
            .SelectMany(drop => Enumerable.Repeat(drop, TreeYield.CountFor(drop, tree.Mesh, kind, scale, Random.Shared.NextDouble)))
            .ToList();
        var shared = YieldShare.Share(items, drop => drop.Value, choppers.Select(entry => (entry.Player, entry.Hits)).ToList());

        foreach (var chopper in choppers)
        {
            var done = chopper.Player == feller ? "Timber!" : "The tree you worked comes down.";
            var mine = shared[chopper.Player];
            if (items.Count == 0)
                Felled(chopper.Player, $"{done} The wood is rotten through: nothing worth taking.");
            else if (mine.Count == 0)
                Felled(chopper.Player, $"{done} Others did most of the work: nothing for you.");
            else
                Give(chopper.Player, chopper.Character, mine.GroupBy(drop => drop).Select(group => (group.Key, group.Count())).ToList(), done);
        }
    }

    private void Give(PlayerId player, MainCharacter character, IReadOnlyList<(TreeDrop Drop, int Count)> drops, string done)
    {
        var given = new List<string>();
        foreach (var (drop, count) in drops)
        {
            _inventories.Add(character.As<Equipment>(), new InventoryEntry(_dropItems[drop], count, 0));
            given.Add($"{count} {drop.Name}");
        }
        Felled(player, $"{done} You get {string.Join(", ", given)}.", string.Join('|', given));
        logger.LogInformation("Jobs: {Player} got {Given}.", player, string.Join(", ", given));
    }

    private void Felled(PlayerId player, string message, string gains = "") => rpc.SendChopEnded(player, (byte)ChopOutcome.Felled, message, gains);

    private void PlaceProps(FelledTree tree)
    {
        props.Place(FelledTreeProps.StumpId(tree.Id), FelledTreeProps.Stump(tree.Id, tree.TreeScale, Settings));
    }

    private void OnDisconnected(Player scope, Guid _, DisconnectReason reason)
    {
        PlayerId player = scope.PlayerId;
        _ready.Remove(player);
        _lastHitAt.Remove(player);
        _lastRefusedAt.Remove(player);
    }

    // Faster than any swing of its kind: a client reporting so is out of step, and the hit is dropped without a word
    private bool TooSoon(PlayerId player, JobAttack attack)
    {
        if (!_lastHitAt.TryGetValue(player, out var last) || Now - last >= HitDamage.MinSecondsBefore(attack, Settings))
            return false;
        logger.LogWarning("Jobs: {Player} reported a {Attack} hit {Seconds:0.00} s after their last. Ignored.", player, attack, Now - last);
        return true;
    }

    // Why this player's hit on this tree does not count, or null when it does
    private string? Refusal(PlayerId player, FoliageId tree, Vector3 at, out MainCharacter character, out Axe? axe, out float condition)
    {
        axe = null;
        condition = 1f;
        if (!entities.TryLookup<MainCharacter, PlayerId>(player, out character))
            return "Your character is not in the world yet.";
        if (character.IsDead)
            return "You cannot chop wood while dead.";
        if (ChopGeometry.HorizontalDistance(character.Position, at) > Settings.Reach + ReachSlack)
        {
            logger.LogWarning("Jobs: {Player} reported a hit at {At}, {Distance:0} cm from where they stand. Ignored.",
                player, at, ChopGeometry.HorizontalDistance(character.Position, at));
            return "You are too far from the tree.";
        }
        if (Settings.LumberCampsOnly && !areas.LumberCamps.Any(camp => camp.Holds(tree.Position)))
            return "This tree is not for felling. Lumber camps are marked on your map.";
        (axe, condition) = BestAxeEquipped(character);
        return _axes.Count > 0 && axe is null ? "Only a battle axe fells a tree." : null;
    }

    // The highest tier axe the character has equipped and its condition, or null for none (and always when no axe is
    // needed)
    private (Axe? Axe, float Condition) BestAxeEquipped(MainCharacter character)
    {
        Axe? best = null;
        var condition = 1f;
        for (var i = 0; i < character.ItemsCount; i++)
        {
            var entry = character.GetItems(i);
            if (entry.Worn != 0 && _axes.TryGetValue(entry.FormId.FormId.Value, out var axe) && (best is null || axe.Tier > best.Tier))
            {
                best = axe;
                condition = HitDamage.ConditionOf(entry.Health, axe.Durability);
            }
        }
        return (best, condition);
    }

    // A broken axe does nothing, and a dull one may glance off: true when this hit does not count. Either way it
    // was a swing, so the next one waits as after any other.
    private bool Dull(PlayerId player, float condition)
    {
        if (condition <= 0f)
        {
            _lastHitAt[player] = Now;
            Refuse(player, "Your axe is too dull to bite.");
            return true;
        }
        if (Random.Shared.NextSingle() >= HitDamage.GlanceChance(condition, Settings))
            return false;
        _lastHitAt[player] = Now;
        rpc.SendChopEnded(player, (byte)ChopOutcome.Glanced, "The blade glances off.", "");
        return true;
    }

    private void Refuse(PlayerId player, string message)
    {
        if (_lastRefusedAt.TryGetValue(player, out var last) && Now - last < RefusalEverySeconds)
            return;
        _lastRefusedAt[player] = Now;
        rpc.SendChopEnded(player, (byte)ChopOutcome.Refused, message, "");
    }

    // A handler's failure is logged, not thrown into the event's other handlers
    private void Guard(string what, PlayerId player, Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Jobs: the {What} of {Player} failed.", what, player);
        }
    }

    private static uint ParseFormId(string text)
    {
        var digits = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? text[2..] : text;
        if (!uint.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            throw new FormatException($"Jobs config: '{text}' is not a form id. Write it in hex, such as 0x00000D7F.");
        return value;
    }
}
