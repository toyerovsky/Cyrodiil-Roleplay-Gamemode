using System.Numerics;
using Microsoft.Extensions.Logging;
using OblivionMP.SDK;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Events;
using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Client;

/// <summary>
/// Mining is hitting: every attack the player lands on an ore node, or on rock inside a quarry, normal or power, goes
/// to the server, which counts its damage if a warhammer is in hand. There is no key and no animation of this mod's
/// own: the game's attacks are the swings. A counted hit wears the hammer by its damage and, with the experimental
/// chips, throws stone off, back towards the miner; a spent node bursts into pieces for everyone near. Rock struck
/// outside any quarry earns a hint, once. Placing nodes is <see cref="PlacementClient"/>'s.
/// </summary>
[Service]
public sealed partial class MiningClient(
    IPlayers players,
    IWeaponHits weaponHits,
    IEquippedWeapon equippedWeapon,
    ChipThrower chips,
    JobsPanel panel,
    MiningClientRpc rpc,
    ILogger logger)
{
    private const string QuarryHint = "This stone won't split cleanly. Quarries are marked on your map.";

    private readonly Dictionary<string, OreNodeView> _nodes = [];
    private readonly List<MarkedArea> _quarries = [];
    private bool _askedForRules;
    private MiningRules? _rules;
    private bool _hinted;

    private void Start()
    {
        rpc.RulesReceived += OnRules;
        rpc.QuarryAvailable += OnQuarryAvailable;
        rpc.NodeAvailable += OnNodeAvailable;
        rpc.NodeRemoved += OnNodeRemoved;
        rpc.Progressed += OnProgressed;
        rpc.Ended += OnEnded;
        rpc.Struck += OnStruck;
        weaponHits.Hit += OnWeaponHit;
    }

    private void Stop()
    {
        rpc.RulesReceived -= OnRules;
        rpc.QuarryAvailable -= OnQuarryAvailable;
        rpc.NodeAvailable -= OnNodeAvailable;
        rpc.NodeRemoved -= OnNodeRemoved;
        rpc.Progressed -= OnProgressed;
        rpc.Ended -= OnEnded;
        rpc.Struck -= OnStruck;
        weaponHits.Hit -= OnWeaponHit;
    }

    private void Update() => FollowConnection();

    // The rules, quarries and nodes come with each connection: what an earlier server sent is dropped
    private void FollowConnection()
    {
        var connected = players.LocalMainCharacter is not null;
        if (connected == _askedForRules)
            return;

        _askedForRules = connected;
        _rules = null;
        _nodes.Clear();
        _quarries.Clear();
        if (connected)
            rpc.SendMiningReady();
    }

    private void OnRules(MiningRules rules)
    {
        _rules = rules;
        logger.LogInformation("Jobs: mining by damage; rock in a quarry is foliage with {Rock} but not {Excluded}{Landscape}; chips {Chips}; placement {Placement}.",
            TreeFilter.Join(rules.QuarryRock.TreeMeshes), TreeFilter.Join(rules.QuarryRock.ExcludedMeshes),
            rules.QuarryLandscape ? ", and the ground" : "", rules.ChipsPerHit > 0 ? "on" : "off", rules.PlacementAllowed ? "on" : "off");
    }

    private void OnQuarryAvailable(MarkedArea quarry)
    {
        _quarries.RemoveAll(other => other.Name == quarry.Name);
        _quarries.Add(quarry);
        logger.LogInformation("Jobs: quarry {Name}, {Radius:0} cm around {Center}.", quarry.Name, quarry.Radius, quarry.Center);
    }

    private void OnNodeAvailable(OreNodeView node) => _nodes[node.Id] = node;

    private void OnNodeRemoved(string id) => _nodes.Remove(id);

    private void OnWeaponHit(WeaponHit hit)
    {
        if (_rules is null || !hit.WithWeapon || AttackOf(hit.Attack) is not { } attack)
            return;

        if (NodeAt(hit.Position) is { } node)
        {
            rpc.SendNodeHit(node.Id, hit.Position.X, hit.Position.Y, hit.Position.Z, (byte)attack);
            logger.LogInformation("Jobs: {Attack} attack on {Node} at {At}.", attack, node.Id, hit.Position);
            return;
        }

        var isRock = hit.Struck switch
        {
            StruckSurface.Foliage => hit.StruckFoliage is { } foliage && _rules.QuarryRock.IsTree(foliage.Mesh),
            StruckSurface.Landscape => _rules.QuarryLandscape,
            StruckSurface.Nothing or StruckSurface.Other => false,
            _ => throw new ArgumentOutOfRangeException(nameof(hit), hit.Struck, "Not a struck surface."),
        };
        if (!isRock)
            return;

        var quarry = _quarries.FirstOrDefault(candidate => candidate.Holds(hit.Position));
        if (quarry.Name is not null)
        {
            rpc.SendQuarryHit(quarry.Name, hit.Position.X, hit.Position.Y, hit.Position.Z, (byte)hit.Struck, hit.StruckFoliage?.Mesh ?? "", (byte)attack);
            logger.LogInformation("Jobs: {Attack} attack on {Rock} in {Quarry} at {At}.", attack, hit.StruckFoliage?.Mesh ?? "the ground", quarry.Name, hit.Position);
            return;
        }

        // A big rock out in the open: once, a word on where stone can be had
        if (!_hinted && hit.Struck == StruckSurface.Foliage)
        {
            _hinted = true;
            panel.Say(PanelLineKind.Note, QuarryHint);
        }
    }

    private static JobAttack? AttackOf(AttackKind kind) => kind switch
    {
        AttackKind.Light => JobAttack.Normal,
        AttackKind.Power => JobAttack.Power,
        AttackKind.Arrow or AttackKind.Other => null,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not an attack kind."),
    };

    // The node whose middle the hit landed within reach of, nearest first
    private OreNodeView? NodeAt(Vector3 at)
        => _nodes.Values
            .Where(node => ChopGeometry.IsOnTrunk(at, node.Position, _rules!.HitRadius * node.Scale))
            .OrderBy(node => ChopGeometry.HorizontalDistance(at, node.Position))
            .Select(node => (OreNodeView?)node)
            .FirstOrDefault();

    private void OnProgressed(MiningProgressNews news)
    {
        // A quarry by its name, a node by its kind: "Iron vein"
        var title = _quarries.Any(quarry => quarry.Name == news.What) ? news.What : $"{news.What} vein";
        panel.Progress(title, news.Damage, news.Health);
        if (news.Wear > 0f && float.IsFinite(news.Wear))
            equippedWeapon.Wear(news.Wear);
    }

    private void OnEnded(MiningOutcome outcome, string message, IReadOnlyList<string> gains)
    {
        switch (outcome)
        {
            case MiningOutcome.Mined:
                panel.Outcome(gains, message);
                break;
            case MiningOutcome.Refused:
                panel.Say(PanelLineKind.Refusal, message);
                break;
            case MiningOutcome.Notice:
                panel.Say(PanelLineKind.Note, message);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Not a mining outcome.");
        }
    }

    /// <summary>A node near this player is spent (<see cref="OreMinedEvent"/>): it bursts into pieces.</summary>
    [EventHandler(Stage.Apply)]
    public void Burst(in OreMinedEvent mined, EventContext context)
    {
        if (mined.Node is not null)
            _nodes.Remove(mined.Node);
        var position = mined.Position;
        var scale = mined.Scale > 0f ? mined.Scale : 1f;
        if (_rules is not { BurstPieces: > 0, ChipAssets.Count: > 0 } rules
            || players.LocalMainCharacter is not { } character
            || ChopGeometry.HorizontalDistance(character.Position, position) > rules.ViewDistance)
            return;

        // The server's prop goes at once; pieces of it fly out where it stood and fall through the ground
        var center = position + new Vector3(0f, 0f, 40f * scale);
        for (var i = 0; i < rules.BurstPieces; i++)
        {
            var path = StoneChip.Burst(center, rules.BurstSpeed * (0.6f + Random.Shared.NextSingle() * 0.8f),
                (i + Random.Shared.NextSingle()) / rules.BurstPieces, Random.Shared.NextSingle(),
                (Random.Shared.NextSingle() - 0.5f) * 1080f, position.Z);
            chips.Throw(rules.ChipAssets, path, rules.BurstScale * scale * (0.6f + Random.Shared.NextSingle() * 0.6f), rules.BurstSeconds);
        }
    }

    private void OnStruck(Vector3 at, Vector2 from, float groundZ)
    {
        if (_rules is not { ChipsPerHit: > 0, ChipAssets.Count: > 0 } rules || players.LocalMainCharacter is not { } character
            || ChopGeometry.HorizontalDistance(character.Position, at) > rules.ViewDistance)
            return;

        chips.ThrowOffHit(rules.ChipAssets, rules.ChipsPerHit, at, from, groundZ, rules.ChipSpeed, rules.ChipScale, rules.ChipSeconds);
    }


}
