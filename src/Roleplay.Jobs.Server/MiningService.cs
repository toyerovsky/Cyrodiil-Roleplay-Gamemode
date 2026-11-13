using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using LiteNetLib;
using Microsoft.Extensions.Logging;
using OblivionMP.SDK.Common.Archetypes;
using OblivionMP.SDK.Common.Archetypes.Mixins;
using OblivionMP.SDK.Common.Props;
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
/// The authority on mining, which is hitting: a client reports each warhammer power attack that lands on an ore node or
/// on rock in a quarry, and this checks the player stands there with a warhammer in hand, counts the hit, and gives.
/// Ore nodes are props this places for everyone (<see cref="IProps"/>): the config's and the ones placed in game. A node
/// is mined out at the hits it needs, from anyone, through <see cref="OreMinedEvent"/>, and what it gives is shared by
/// those hits, the best of it to whoever struck most (<see cref="OreYield.Share"/>); it grows back after its kind's
/// respawn time. A quarry gives each player for their own hits, every few of them, through
/// <see cref="StoneQuarriedEvent"/>, and does not run out.
/// </summary>
[Service]
public sealed partial class MiningService(
    IEntities entities,
    IServerEvents serverEvents,
    INetworkedEvents events,
    IProps props,
    MiningRpcHandlers rpc,
    OreNodeStore store,
    JobAreas areas,
    JobsConfig config,
    ILogger logger)
{
    private readonly record struct Standing(OreNode Node, OreKind Kind, bool Placed);

    // The client reports a hit a moment after it landed, from where it stood then
    private const float ReachSlack = 100f;
    // The client judges a hit on the same node; the slack only covers float rounding on the wire
    private const float HitSlack = 10f;
    // Placement puts a node this far in front of the player, and removes a placed one within this of that spot
    // How far from the player a node may be placed or removed
    private const float PlaceReach = 600f;
    // A refusal is said at most this often to one player: every power attack with a sword near a node would repeat it
    private const double RefusalEverySeconds = 8;

    private readonly HashSet<PlayerId> _ready = [];
    private readonly Dictionary<string, Standing> _nodes = [];
    private readonly Dictionary<string, HitTally<PlayerId>> _damage = [];
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
        rpc.RemoveRequested += OnRemove;
        serverEvents.OnPlayerDisconnected += OnDisconnected;

        logger.LogInformation("Jobs: mining running. {Nodes} ore nodes ({Mined} mined out), {Kinds} kinds, {Hammers} warhammers, {Quarries} quarries; placement {Placement}, chips {Chips}.",
            _nodes.Count, store.Mined.Count, Settings.Kinds.Count, _hammers.Count, areas.Quarries.Count,
            Settings.AllowNodePlacement ? "on" : "off", Settings.ExperimentalChips ? "on" : "off");
    }

    private void Stop()
    {
        rpc.Ready -= OnReady;
        rpc.NodeHitReported -= OnNodeHit;
        rpc.QuarryHitReported -= OnQuarryHit;
        rpc.PlaceRequested -= OnPlace;
        rpc.RemoveRequested -= OnRemove;
        serverEvents.OnPlayerDisconnected -= OnDisconnected;
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
        rpc.SendMiningRules(player, Settings.HitRadius, TreeFilter.Join(_rockFilter.TreeMeshes), TreeFilter.Join(_rockFilter.ExcludedMeshes),
            Settings.QuarryLandscape, Settings.ViewDistance, Settings.ExperimentalChips ? Settings.ChipsPerHit : 0,
            string.Join('|', Settings.ChipAssets), Settings.ChipSpeed, Settings.ChipSeconds, Settings.ChipScale, Settings.BurstPieces,
            Settings.BurstSpeed, Settings.BurstSeconds, Settings.BurstScale, Settings.AllowNodePlacement,
            string.Join('|', Settings.Kinds.Select(kind => kind.Name)), string.Join('|', Settings.Kinds.Select(kind => kind.Asset)),
            string.Join('|', Settings.Kinds.Select(kind => kind.Scale.ToString(CultureInfo.InvariantCulture))));
        foreach (var quarry in areas.Quarries)
            rpc.SendQuarryAvailable(player, quarry.Name, quarry.Center.X, quarry.Center.Y, quarry.Center.Z, quarry.Radius);
        foreach (var (id, standing) in _nodes)
            if (!store.Mined.ContainsKey(id))
                SendAvailable(player, id, standing);
    }

    private void OnNodeHit(PlayerId player, string id, Vector3 at, byte attackCode)
    {
        if (AttackOf(player, attackCode) is not { } attack)
            return;
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
        if (TooSoon(player, attack))
            return;
        if (Refusal(player, at, out var character, out var hammer, out var condition) is { } refusal)
        {
            Refuse(player, refusal);
            return;
        }
        if (hammer is not null && Dull(player, condition))
            return;

        var health = OreYield.HealthFor(standing.Kind, standing.Node.Scale, hammer?.Tier, Settings);
        var dealt = HitDamage.DamageOf(attack, HitDamage.StrengthOf(character), HitDamage.TiersAbove(hammer?.Tier, standing.Kind.Tier), Settings);
        if (!_damage.TryGetValue(id, out var damage))
            _damage[id] = damage = new HitTally<PlayerId>();
        damage.Add(player, Now, dealt);
        Counted(player, at, character);
        rpc.SendMiningProgress(player, standing.Kind.Name, Math.Min(damage.Hits, health), health, WearOf(dealt));
        if (damage.Hits < health)
            return;

        if (!character.TryAs(out Character miner))
            return;
        events.RaiseAs(player, new OreMinedEvent
        {
            Miner = miner, Node = id, Kind = standing.Kind.Name, Position = OreYield.PositionOf(standing.Node),
            Scale = standing.Kind.Scale * standing.Node.Scale,
        });
    }

    /// <summary>A node is mined out only while it stands: two last hits at once spend it once.</summary>
    [EventHandler(Stage.Validate)]
    public void CheckMined(in OreMinedEvent mined, EventContext context)
    {
        if (mined.Node is null || !_nodes.ContainsKey(mined.Node))
            context.Cancel(HarvestRefusal.Other);
        else if (store.Mined.ContainsKey(mined.Node))
            context.Cancel(HarvestRefusal.Gone);
    }

    /// <summary>A spent node that passed every check: it goes until it grows back, and what it gives is shared among
    /// everyone who hit it.</summary>
    [EventHandler(Stage.Apply)]
    public void Mine(in OreMinedEvent mined, EventContext context)
    {
        var id = mined.Node;
        var miner = JobEvents.PlayerOf(mined.Miner);
        Guard("mining", miner, () => Mine(id, miner));
    }

    [EventHandler(Stage.Rejected)]
    public void MiningRefused(in OreMinedEvent mined, HarvestRefusal reason, EventContext context)
    {
        var miner = JobEvents.PlayerOf(mined.Miner);
        if (reason == HarvestRefusal.Gone && mined.Node is not null)
            _damage.Remove(mined.Node);
        logger.LogInformation("Jobs: mining out {Node} by {Player} refused: {Reason}.", mined.Node, miner, reason);
        rpc.SendMiningEnded(miner, (byte)MiningOutcome.Refused, JobEvents.Describe(reason), "");
    }

    private void Mine(string id, PlayerId finisher)
    {
        var standing = _nodes[id];
        var damage = _damage.Remove(id, out var hits) ? hits : null;
        if (damage is null)
        {
            // Raised by another mod with no hits behind it: the miner gets it all
            damage = new HitTally<PlayerId>();
            damage.Add(finisher, Now);
        }

        props.Remove(id);
        store.SetMined(id, DateTime.UtcNow.AddMinutes(standing.Kind.RespawnMinutes));
        ShareYield(standing, damage, finisher);
        logger.LogInformation("Jobs: {Player} mined out {Node}, after {Hits}.", finisher, id, damage);
    }

    // The node's yield, rolled once, shared by everyone who struck it and is still in the world
    private void ShareYield(Standing standing, HitTally<PlayerId> damage, PlayerId finisher)
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
            if (mine.Count == 0 && items.Count > 0)
            {
                rpc.SendMiningEnded(miner.Player, (byte)MiningOutcome.Mined, $"{done} Others did most of the work: nothing for you.", "");
                continue;
            }
            Give(miner.Player, miner.Character, mine.GroupBy(drop => drop).Select(group => (group.Key, group.Count())).ToList(), done);
        }
    }

    private void OnQuarryHit(PlayerId player, string name, Vector3 at, byte struck, string mesh, byte attackCode)
    {
        if (AttackOf(player, attackCode) is not { } attack)
            return;
        var quarry = areas.Quarries.FirstOrDefault(candidate => candidate.Name == name);
        if (quarry.Name is null || !quarry.Holds(at))
        {
            logger.LogWarning("Jobs: {Player} reported a hit at {At} in quarry {Quarry}, which is no quarry there. Ignored.", player, at, name);
            return;
        }
        if (!Enum.IsDefined((StruckKind)struck) || !IsQuarryRock((StruckKind)struck, mesh))
        {
            logger.LogWarning("Jobs: {Player} reported a quarry hit on {Struck} {Mesh}, which is no rock to quarry. Ignored.", player, struck, mesh);
            return;
        }
        if (TooSoon(player, attack))
            return;
        if (Refusal(player, at, out var character, out var quarryHammer, out var quarryCondition) is { } refusal)
        {
            Refuse(player, refusal);
            return;
        }
        if (quarryHammer is not null && Dull(player, quarryCondition))
            return;

        // A block comes loose at every QuarryDamagePerYield of the player's damage; what is over counts towards the next
        var key = (player, quarry.Name);
        var perYield = Math.Max(1, Settings.QuarryDamagePerYield);
        var dealt = HitDamage.DamageOf(attack, HitDamage.StrengthOf(character), tiersAbove: 0, Settings);
        var total = (_quarried.TryGetValue(key, out var done) ? done : 0) + dealt;
        Counted(player, at, character);
        rpc.SendMiningProgress(player, quarry.Name, Math.Min(total, perYield), perYield, WearOf(dealt));
        if (total < perYield)
        {
            _quarried[key] = total;
            return;
        }

        _quarried[key] = total - perYield;
        if (!character.TryAs(out Character quarrier))
            return;
        events.RaiseAs(player, new StoneQuarriedEvent { Quarrier = quarrier, Quarry = quarry.Name, Position = at });
    }

    [EventHandler(Stage.Validate)]
    public void CheckQuarried(in StoneQuarriedEvent quarried, EventContext context)
    {
        var name = quarried.Quarry;
        if (name is null || !areas.Quarries.Any(quarry => quarry.Name == name))
            context.Cancel(HarvestRefusal.Other);
    }

    /// <summary>A block that came loose and passed every check: the quarrier gets the quarry's yield.</summary>
    [EventHandler(Stage.Apply)]
    public void Quarry(in StoneQuarriedEvent quarried, EventContext context)
    {
        var player = JobEvents.PlayerOf(quarried.Quarrier);
        var name = quarried.Quarry;
        Guard("quarrying", player, () =>
        {
            if (!entities.TryLookup<MainCharacter, PlayerId>(player, out var character))
                return;
            var rolled = Settings.QuarryDrops.Select(drop => (drop, OreYield.CountFor(drop, 1f, Random.Shared.NextDouble))).ToList();
            Give(player, character, rolled, "A block comes loose.");
            logger.LogInformation("Jobs: {Player} quarried a block in {Quarry}.", player, name);
        });
    }

    [EventHandler(Stage.Rejected)]
    public void QuarryingRefused(in StoneQuarriedEvent quarried, HarvestRefusal reason, EventContext context)
    {
        var player = JobEvents.PlayerOf(quarried.Quarrier);
        logger.LogInformation("Jobs: a block in {Quarry} for {Player} refused: {Reason}.", quarried.Quarry, player, reason);
        rpc.SendMiningEnded(player, (byte)MiningOutcome.Refused, JobEvents.Describe(reason), "");
    }

    private bool IsQuarryRock(StruckKind struck, string mesh) => struck switch
    {
        StruckKind.Foliage => _rockFilter.IsTree(mesh),
        StruckKind.Landscape => Settings.QuarryLandscape,
        StruckKind.Nothing or StruckKind.Other => false,
        _ => throw new ArgumentOutOfRangeException(nameof(struck), struck, "Not a struck kind."),
    };

    // Faster than any swing of its kind: a client reporting so is out of step, and the hit is dropped without a word
    private bool TooSoon(PlayerId player, JobAttack attack)
    {
        if (!_lastHitAt.TryGetValue(player, out var last) || Now - last >= HitDamage.MinSecondsBefore(attack, Settings))
            return false;
        logger.LogWarning("Jobs: {Player} reported a {Attack} hit {Seconds:0.00} s after their last. Ignored.", player, attack, Now - last);
        return true;
    }

    private JobAttack? AttackOf(PlayerId player, byte attackCode)
    {
        if (Enum.IsDefined((JobAttack)attackCode))
            return (JobAttack)attackCode;
        logger.LogWarning("Jobs: {Player} reported a hit by attack {Attack}, which is none. Ignored.", player, attackCode);
        return null;
    }

    // The hammer's wear for a hit of this damage; none when no hammer is needed
    private float WearOf(int damage) => _hammers.Count > 0 ? Settings.WearPerDamage * damage : 0f;

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
        rpc.SendMiningEnded(player, (byte)MiningOutcome.Mined, $"{done} {what}", string.Join('|', given));
        logger.LogInformation("Jobs: {Player} got {Given}.", player, given.Count > 0 ? string.Join(", ", given) : "nothing");
    }

    // Why this player's hit at this point does not count, or null when it does
    private string? Refusal(PlayerId player, Vector3 at, out MainCharacter character, out Hammer? hammer, out float condition)
    {
        hammer = null;
        condition = 1f;
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
        (hammer, condition) = BestHammerEquipped(character);
        return _hammers.Count > 0 && hammer is null ? "Only a warhammer breaks stone." : null;
    }

    // A broken warhammer does nothing, and a blunt one may glance off: true when this hit does not count. Either way
    // it was a swing, so the next one waits as after any other.
    private bool Dull(PlayerId player, float condition)
    {
        if (condition <= 0f)
        {
            _lastHitAt[player] = Now;
            Refuse(player, "Your warhammer is too blunt to bite.");
            return true;
        }
        if (Random.Shared.NextSingle() >= HitDamage.GlanceChance(condition, Settings))
            return false;
        _lastHitAt[player] = Now;
        Notice(player, "The hammer glances off.");
        return true;
    }

    private bool MayPlace(PlayerId player)
    {
        if (Settings.AllowNodePlacement)
            return true;
        logger.LogWarning("Jobs: {Player} tried to place or remove an ore node, which this server does not allow.", player);
        Notice(player, "This server does not let you place ore nodes.");
        return false;
    }

    // Just where the player's preview is, nothing in the way checked: for laying out a quarry by eye
    private void OnPlace(PlayerId player, int kindIndex, Vector3 spot, float yaw, float pitch)
    {
        if (!MayPlace(player) || !entities.TryLookup<MainCharacter, PlayerId>(player, out var character))
            return;
        if (kindIndex < 0 || kindIndex >= Settings.Kinds.Count)
        {
            Notice(player, "There is no kind of ore node there to place.");
            return;
        }
        if (!float.IsFinite(spot.X) || !float.IsFinite(spot.Y) || !float.IsFinite(spot.Z) || !float.IsFinite(yaw) || !float.IsFinite(pitch)
            || Vector3.Distance(character.Position, spot) > PlaceReach)
        {
            logger.LogWarning("Jobs: {Player} at {Standing} asked for an ore node at {At}, out of reach.", player, character.Position, spot);
            Notice(player, "That is too far to place an ore node.");
            return;
        }

        var kind = Settings.Kinds[kindIndex];
        var node = new OreNode { Kind = kind.Name, X = spot.X, Y = spot.Y, Z = spot.Z, Yaw = yaw, Pitch = pitch, Scale = 1f };
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

    private void OnRemove(PlayerId player)
    {
        if (!MayPlace(player) || !entities.TryLookup<MainCharacter, PlayerId>(player, out var character))
            return;
        var standing = character.Position;
        var near = _nodes.Where(pair => pair.Value.Placed && Vector3.Distance(OreYield.PositionOf(pair.Value.Node), standing) <= PlaceReach)
            .OrderBy(pair => Vector3.Distance(OreYield.PositionOf(pair.Value.Node), standing))
            .Select(pair => pair.Key)
            .FirstOrDefault();
        if (near is null)
        {
            Notice(player, "There is no placed ore node near enough to remove.");
            return;
        }
        var kind = _nodes[near].Kind.Name;
        Remove(near);
        Notice(player, $"Removed the {kind} node.");
        logger.LogInformation("Jobs: {Player} removed {Node}.", player, near);
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

    // The highest tier warhammer the character has equipped and its condition, or null for none (and always when none
    // is needed)
    private (Hammer? Hammer, float Condition) BestHammerEquipped(MainCharacter character)
    {
        Hammer? best = null;
        var condition = 1f;
        for (var i = 0; i < character.ItemsCount; i++)
        {
            var entry = character.GetItems(i);
            if (entry.Worn != 0 && _hammers.TryGetValue(entry.FormId.FormId.Value, out var hammer) && (best is null || hammer.Tier > best.Tier))
            {
                best = hammer;
                condition = HitDamage.ConditionOf(entry.Health, hammer.Durability);
            }
        }
        return (best, condition);
    }

    private float HitRadiusOf(Standing standing) => Settings.HitRadius * standing.Kind.Scale * standing.Node.Scale;

    private Prop PropOf(Standing standing)
        => new(standing.Kind.Asset, OreYield.PositionOf(standing.Node), new Vector3(standing.Node.Pitch, standing.Node.Yaw, 0),
            standing.Kind.Scale * standing.Node.Scale);

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
        rpc.SendMiningEnded(player, (byte)MiningOutcome.Refused, message, "");
    }

    private void Notice(PlayerId player, string message) => rpc.SendMiningEnded(player, (byte)MiningOutcome.Notice, message, "");

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
            throw new FormatException($"Jobs config: '{text}' is not a form id. Write it in hex, such as 0x00019172.");
        return value;
    }
}
