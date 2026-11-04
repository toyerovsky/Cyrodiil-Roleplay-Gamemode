using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using LiteNetLib;
using Microsoft.Extensions.Logging;
using OblivionMp.Sdk.Common.Archetypes;
using OblivionMp.Sdk.Common.Archetypes.Mixins;
using OblivionMp.Sdk.Common.Props;
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
/// The authority on mining, which is hitting: a client reports each warhammer power attack that lands on an ore node or
/// on rock in a quarry, and this checks the player stands there with a warhammer in hand, counts the hit, and gives.
/// Ore nodes are props this places for everyone (<see cref="IProps"/>): the config's and the ones placed in game. A node
/// is mined out at the hits it needs, from anyone, and what it gives is shared by those hits, the best of it to whoever
/// struck most (<see cref="OreYield.Share"/>); it grows back after its kind's respawn time. A quarry gives each player
/// for their own hits, every few of them, and does not run out.
/// </summary>
[Service]
public sealed partial class MiningService(
    IEntities entities,
    IServerEvents events,
    IProps props,
    MiningRpcHandlers rpc,
    OreNodeStore store,
    QuarryRegistry quarries,
    JobsConfig config,
    ILogger logger)
{
    private readonly record struct Standing(OreNode Node, OreKind Kind, bool Placed);

    // Every player's hits on one node, in the order they first struck
    private sealed class Damage
    {
        public readonly List<(PlayerId Player, int Hits)> ByPlayer = [];
        public double LastHitAt;

        public int Hits => ByPlayer.Sum(entry => entry.Hits);

        public void Add(PlayerId player, double at)
        {
            LastHitAt = at;
            var index = ByPlayer.FindIndex(entry => entry.Player == player);
            if (index < 0)
                ByPlayer.Add((player, 1));
            else
                ByPlayer[index] = (player, ByPlayer[index].Hits + 1);
        }
    }

    // The client reports a hit a moment after it landed, from where it stood then
    private const float ReachSlack = 100f;
    // The client judges a hit on the same node; the slack only covers float rounding on the wire
    private const float HitSlack = 10f;
    // Placement puts a node this far in front of the player, and removes a placed one within this of that spot
    private const float PlaceAhead = 150f;
    // A refusal is said at most this often to one player: every power attack with a sword near a node would repeat it
    private const double RefusalEverySeconds = 8;

    private readonly HashSet<PlayerId> _ready = [];
    private readonly Dictionary<string, Standing> _nodes = [];
    private readonly Dictionary<string, Damage> _damage = [];
    private readonly Dictionary<(PlayerId Player, string Quarry), int> _quarried = [];
    private readonly Dictionary<PlayerId, double> _lastHitAt = [];
    private readonly Dictionary<PlayerId, double> _lastRefusedAt = [];
    private readonly Inventories _inventories = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private MiningConfig Settings => config.Mining;
    private Dictionary<uint, Hammer> _hammers = [];
    private Dictionary<OreDrop, FormId> _dropItems = [];
    private TreeFilter _rockFilter = new([], []);
    private double _nextRegrowCheck;

    private double Now => _clock.Elapsed.TotalSeconds;

    private void Start()
    {
        _hammers = Settings.Hammers.ToDictionary(hammer => ParseFormId(hammer.Item));
        _dropItems = Settings.Kinds.SelectMany(kind => kind.Drops).Concat(Settings.QuarryDrops)
            .ToDictionary(drop => drop, drop => new FormId(ParseFormId(drop.Item)));
        _rockFilter = new TreeFilter(Settings.QuarryMeshes, Settings.ExcludedQuarryMeshes);

        foreach (var node in Settings.Nodes)
            Add(node, placed: false);
        foreach (var node in store.Placed)
            Add(node, placed: true);
        foreach (var (id, standing) in _nodes)
            if (!store.Mined.ContainsKey(id))
                props.Place(id, PropOf(standing));

        rpc.Ready += OnReady;
        rpc.NodeHitReported += OnNodeHit;
        rpc.QuarryHitReported += OnQuarryHit;
        rpc.PlaceRequested += OnPlace;
        events.OnPlayerDisconnected += OnDisconnected;

        logger.LogInformation("Jobs: mining running. {Nodes} ore nodes ({Mined} mined out), {Kinds} kinds, {Hammers} warhammers, {Quarries} quarries; placement {Placement}, chips {Chips}.",
            _nodes.Count, store.Mined.Count, Settings.Kinds.Count, _hammers.Count, quarries.Quarries.Count,
            Settings.AllowNodePlacement ? "on" : "off", Settings.ExperimentalChips ? "on" : "off");
    }

    private void Stop()
    {
        rpc.Ready -= OnReady;
        rpc.NodeHitReported -= OnNodeHit;
        rpc.QuarryHitReported -= OnQuarryHit;
        rpc.PlaceRequested -= OnPlace;
        events.OnPlayerDisconnected -= OnDisconnected;
    }

    private void Update()
    {
        if (Now < _nextRegrowCheck)
            return;
        _nextRegrowCheck = Now + 1;

        var now = DateTime.UtcNow;
        foreach (var (id, _) in store.Mined.Where(pair => pair.Value <= now).ToList())
        {
            store.ClearMined(id);
            if (!_nodes.TryGetValue(id, out var standing))
                continue; // its node left the config: nothing to grow back
            props.Place(id, PropOf(standing));
            foreach (var player in _ready)
                SendAvailable(player, id, standing);
            logger.LogInformation("Jobs: {Node} grew back.", id);
        }

        foreach (var (id, _) in _damage.Where(pair => Now - pair.Value.LastHitAt > Settings.NodeMemorySeconds).ToList())
            _damage.Remove(id);
    }

    private void OnReady(PlayerId player)
    {
        _ready.Add(player);
        rpc.SendMiningRules(player, Settings.HitRadius, _hammers.Count > 0 ? Settings.WearPerHit : 0f, TreeFilter.Join(_rockFilter.TreeMeshes), TreeFilter.Join(_rockFilter.ExcludedMeshes),
            Settings.QuarryLandscape, Settings.ViewDistance, Settings.ExperimentalChips ? Settings.ChipsPerHit : 0,
            string.Join('|', Settings.ChipAssets), Settings.ChipSpeed, Settings.ChipSeconds, Settings.ChipScale, Settings.BurstPieces,
            Settings.BurstSpeed, Settings.BurstSeconds, Settings.BurstScale, Settings.AllowNodePlacement,
            string.Join('|', Settings.Kinds.Select(kind => kind.Name)));
        foreach (var quarry in quarries.Quarries)
            rpc.SendQuarryAvailable(player, quarry.Name, quarry.Center.X, quarry.Center.Y, quarry.Center.Z, quarry.Radius);
        foreach (var (id, standing) in _nodes)
            if (!store.Mined.ContainsKey(id))
                SendAvailable(player, id, standing);
    }

    private void OnNodeHit(PlayerId player, string id, Vector3 at)
    {
        if (!_nodes.TryGetValue(id, out var standing) || store.Mined.ContainsKey(id))
        {
            logger.LogDebug("Jobs: {Player} hit {Node}, which is no standing node. Ignored.", player, id);
            return;
        }
        if (!ChopGeometry.IsOnTrunk(at, OreYield.PositionOf(standing.Node), HitRadiusOf(standing) + HitSlack))
        {
            logger.LogWarning("Jobs: {Player} reported a hit at {At}, which is off {Node}. Ignored.", player, at, id);
            return;
        }
        if (TooSoon(player))
            return;
        if (Refusal(player, at, out var character, out var hammer) is { } refusal)
        {
            Refuse(player, refusal);
            return;
        }

        var hitsNeeded = OreYield.HitsFor(standing.Kind, standing.Node.Scale, hammer?.Tier, Settings);
        if (!_damage.TryGetValue(id, out var damage))
            _damage[id] = damage = new Damage();
        damage.Add(player, Now);
        Counted(player, at, character);
        if (damage.Hits < hitsNeeded)
        {
            rpc.SendMiningProgress(player, standing.Kind.Name, damage.Hits, hitsNeeded);
            return;
        }

        _damage.Remove(id);
        props.Remove(id);
        store.SetMined(id, DateTime.UtcNow.AddMinutes(standing.Kind.RespawnMinutes));
        foreach (var ready in _ready)
            rpc.SendNodeMined(ready, id, ready == player);
        ShareYield(standing, damage, finisher: player);
        logger.LogInformation("Jobs: {Player} mined out {Node} with {Hammer}, after {Hits}.", player, id, hammer?.Name ?? "no warhammer needed",
            string.Join(", ", damage.ByPlayer.Select(entry => $"{entry.Player}: {entry.Hits}")));
    }

    // The node's yield, rolled once, shared by everyone who struck it and is still in the world
    private void ShareYield(Standing standing, Damage damage, PlayerId finisher)
    {
        var miners = damage.ByPlayer
            .Select(entry => (entry.Player, entry.Hits, Found: entities.TryLookup<MainCharacter, PlayerId>(entry.Player, out var character), Character: character))
            .Where(entry => entry.Found)
            .ToList();
        var items = standing.Kind.Drops
            .SelectMany(drop => Enumerable.Repeat(drop, OreYield.CountFor(drop, standing.Node.Scale, Random.Shared.NextDouble)))
            .ToList();
        var shared = OreYield.Share(items, miners.Select(entry => (entry.Player, entry.Hits)).ToList());

        foreach (var miner in miners)
        {
            var done = miner.Player == finisher ? "The vein is spent." : "The vein you worked is spent.";
            var mine = shared[miner.Player];
            if (mine.Count == 0)
            {
                rpc.SendMiningEnded(miner.Player, (byte)MiningOutcome.Mined, $"{done} Others did most of the work: nothing for you.");
                continue;
            }
            Give(miner.Player, miner.Character, mine.GroupBy(drop => drop).Select(group => (group.Key, group.Count())).ToList(), done);
        }
    }

    private void OnQuarryHit(PlayerId player, string name, Vector3 at, byte struck, string mesh)
    {
        var quarry = quarries.Quarries.FirstOrDefault(candidate => candidate.Name == name);
        if (quarry is null || !quarry.Holds(at))
        {
            logger.LogWarning("Jobs: {Player} reported a hit at {At} in quarry {Quarry}, which is no quarry there. Ignored.", player, at, name);
            return;
        }
        if (!Enum.IsDefined((StruckKind)struck) || !IsQuarryRock((StruckKind)struck, mesh))
        {
            logger.LogWarning("Jobs: {Player} reported a quarry hit on {Struck} {Mesh}, which is no rock to quarry. Ignored.", player, struck, mesh);
            return;
        }
        if (TooSoon(player))
            return;
        if (Refusal(player, at, out var character, out _) is { } refusal)
        {
            Refuse(player, refusal);
            return;
        }

        var key = (player, quarry.Name);
        var hits = (_quarried.TryGetValue(key, out var done) ? done : 0) + 1;
        Counted(player, at, character);
        if (hits < Settings.QuarryHitsPerYield)
        {
            _quarried[key] = hits;
            rpc.SendMiningProgress(player, quarry.Name, hits, Settings.QuarryHitsPerYield);
            return;
        }

        _quarried.Remove(key);
        var rolled = Settings.QuarryDrops.Select(drop => (drop, OreYield.CountFor(drop, 1f, Random.Shared.NextDouble))).ToList();
        Give(player, character, rolled, "A block comes loose.");
    }

    private bool IsQuarryRock(StruckKind struck, string mesh) => struck switch
    {
        StruckKind.Foliage => _rockFilter.IsTree(mesh),
        StruckKind.Landscape => Settings.QuarryLandscape,
        StruckKind.Nothing or StruckKind.Other => false,
        _ => throw new ArgumentOutOfRangeException(nameof(struck), struck, "Not a struck kind."),
    };

    // Faster than any swing: a client reporting so is out of step, and the hit is dropped without a word
    private bool TooSoon(PlayerId player)
    {
        if (!_lastHitAt.TryGetValue(player, out var last) || Now - last >= Settings.MinSecondsBetweenHits)
            return false;
        logger.LogWarning("Jobs: {Player} reported hits {Seconds:0.00} s apart. Ignored.", player, Now - last);
        return true;
    }

    // A hit that counts: the players around see stone fly
    private void Counted(PlayerId player, Vector3 at, MainCharacter character)
    {
        _lastHitAt[player] = Now;
        if (!Settings.ExperimentalChips)
            return;
        foreach (var ready in _ready)
            rpc.SendStruck(ready, at.X, at.Y, at.Z, character.Position.X, character.Position.Y, character.Position.Z);
    }

    private void Give(PlayerId player, MainCharacter character, IReadOnlyList<(OreDrop Drop, int Count)> drops, string done)
    {
        var given = new List<string>();
        foreach (var (drop, count) in drops)
        {
            if (count <= 0)
                continue;
            _inventories.Add(character.As<Equipment>(), new InventoryEntry(_dropItems[drop], count, 0));
            given.Add($"{count} {drop.Name}");
        }

        var what = given.Count > 0 ? $"You get {string.Join(", ", given)}." : "Nothing but dust.";
        rpc.SendMiningEnded(player, (byte)MiningOutcome.Mined, $"{done} {what}");
        logger.LogInformation("Jobs: {Player} got {Given}.", player, given.Count > 0 ? string.Join(", ", given) : "nothing");
    }

    // Why this player's hit at this point does not count, or null when it does
    private string? Refusal(PlayerId player, Vector3 at, out MainCharacter character, out Hammer? hammer)
    {
        hammer = null;
        if (!entities.TryLookup<MainCharacter, PlayerId>(player, out character))
            return "Your character is not in the world yet.";
        if (character.IsDead)
            return "You cannot mine while dead.";
        if (ChopGeometry.HorizontalDistance(character.Position, at) > Settings.Reach + ReachSlack)
        {
            logger.LogWarning("Jobs: {Player} reported a hit at {At}, {Distance:0} cm from where they stand. Ignored.",
                player, at, ChopGeometry.HorizontalDistance(character.Position, at));
            return "You are too far away.";
        }
        hammer = BestHammerEquipped(character);
        return _hammers.Count > 0 && hammer is null ? "Only a warhammer breaks stone." : null;
    }

    private void OnPlace(PlayerId player, int kindIndex)
    {
        if (!Settings.AllowNodePlacement)
        {
            logger.LogWarning("Jobs: {Player} tried to place an ore node, which this server does not allow.", player);
            Notice(player, "This server does not let you place ore nodes.");
            return;
        }
        if (!entities.TryLookup<MainCharacter, PlayerId>(player, out var character))
            return;
        if (Settings.Kinds.Count == 0)
        {
            Notice(player, "There are no kinds of ore node to place.");
            return;
        }

        var spot = Ahead(character.Position, character.Rotation, PlaceAhead);
        var near = _nodes.Where(pair => ChopGeometry.HorizontalDistance(OreYield.PositionOf(pair.Value.Node), spot) <= PlaceAhead)
            .OrderBy(pair => ChopGeometry.HorizontalDistance(OreYield.PositionOf(pair.Value.Node), spot))
            .Select(pair => (Id: pair.Key, pair.Value))
            .FirstOrDefault();
        if (near.Id is not null)
        {
            if (!near.Value.Placed)
            {
                Notice(player, "That node is in the config: remove it there.");
                return;
            }
            Remove(near.Id);
            Notice(player, $"Removed the {near.Value.Kind.Name} node.");
            logger.LogInformation("Jobs: {Player} removed {Node}.", player, near.Id);
            return;
        }

        var kind = Settings.Kinds[((kindIndex % Settings.Kinds.Count) + Settings.Kinds.Count) % Settings.Kinds.Count];
        // Turned to face the player who placed it
        var node = new OreNode { Kind = kind.Name, X = spot.X, Y = spot.Y, Z = spot.Z, Yaw = character.Rotation + 180f, Scale = 1f };
        var id = OreYield.NodeId(node);
        if (_nodes.ContainsKey(id))
        {
            Notice(player, "There is a node there already.");
            return;
        }

        store.AddPlaced(node);
        var standing = Add(node, placed: true);
        props.Place(id, PropOf(standing));
        foreach (var ready in _ready)
            SendAvailable(ready, id, standing);
        Notice(player, $"Placed a {kind.Name} node.");
        logger.LogInformation("Jobs: {Player} placed {Node}.", player, id);
    }

    private void OnDisconnected(Player scope, Guid _, DisconnectReason reason)
    {
        PlayerId player = scope.PlayerId;
        _ready.Remove(player);
        _lastHitAt.Remove(player);
        _lastRefusedAt.Remove(player);
        foreach (var key in _quarried.Keys.Where(key => key.Player == player).ToList())
            _quarried.Remove(key);
    }

    private Standing Add(OreNode node, bool placed)
    {
        var kind = Settings.Kinds.FirstOrDefault(candidate => candidate.Name == node.Kind)
                   ?? throw new InvalidOperationException($"Jobs config: an ore node at ({node.X}, {node.Y}, {node.Z}) is of kind '{node.Kind}', which Kinds does not list.");
        var standing = new Standing(node, kind, placed);
        _nodes[OreYield.NodeId(node)] = standing;
        return standing;
    }

    private void Remove(string id)
    {
        _nodes.Remove(id);
        _damage.Remove(id);
        store.RemovePlaced(id);
        props.Remove(id);
        foreach (var ready in _ready)
            rpc.SendNodeRemoved(ready, id);
    }

    // The highest tier warhammer the character has equipped, or null for none (and always when none is needed)
    private Hammer? BestHammerEquipped(MainCharacter character)
    {
        Hammer? best = null;
        for (var i = 0; i < character.ItemsCount; i++)
        {
            var entry = character.GetItems(i);
            if (entry.Worn != 0 && _hammers.TryGetValue(entry.FormId.FormId.Value, out var hammer) && (best is null || hammer.Tier > best.Tier))
                best = hammer;
        }
        return best;
    }

    private float HitRadiusOf(Standing standing) => Settings.HitRadius * standing.Kind.Scale * standing.Node.Scale;

    private Prop PropOf(Standing standing)
        => new(standing.Kind.Asset, OreYield.PositionOf(standing.Node), new Vector3(0, standing.Node.Yaw, 0), standing.Kind.Scale * standing.Node.Scale);

    private void SendAvailable(PlayerId player, string id, Standing standing)
        => rpc.SendNodeAvailable(player, id, standing.Kind.Name, standing.Kind.Asset, standing.Node.X, standing.Node.Y, standing.Node.Z,
            standing.Node.Yaw, standing.Kind.Scale * standing.Node.Scale);

    private static Vector3 Ahead(Vector3 at, float yawDegrees, float distance)
    {
        var (sin, cos) = MathF.SinCos(yawDegrees * MathF.PI / 180f);
        return at + new Vector3(cos * distance, sin * distance, 0f);
    }

    private void Refuse(PlayerId player, string message)
    {
        if (_lastRefusedAt.TryGetValue(player, out var last) && Now - last < RefusalEverySeconds)
            return;
        _lastRefusedAt[player] = Now;
        rpc.SendMiningEnded(player, (byte)MiningOutcome.Refused, message);
    }

    private void Notice(PlayerId player, string message) => rpc.SendMiningEnded(player, (byte)MiningOutcome.Notice, message);

    private static uint ParseFormId(string text)
    {
        var digits = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? text[2..] : text;
        if (!uint.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            throw new FormatException($"Jobs config: '{text}' is not a form id. Write it in hex, such as 0x00019172.");
        return value;
    }
}
