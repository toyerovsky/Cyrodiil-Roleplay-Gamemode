using System.Diagnostics;
using System.Numerics;
using Microsoft.Extensions.Logging;
using OblivionMP.SDK;
using OblivionMP.SDK.Common.Props;
using OblivionMpCSharpMod;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Events;
using OblivionMpCSharpMod.Input;
using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Client;

/// <summary>
/// Mining is hitting: every power attack the player lands on an ore node, or on rock inside a quarry, goes to the
/// server, which counts it if a warhammer is in hand. There is no key and no animation of this mod's own: the game's
/// power attack is the swing. A counted hit wears the hammer a little and, with the experimental chips, throws stone
/// off; a spent node bursts into pieces for everyone near. Rock struck outside any quarry earns a hint, once. With
/// placement allowed, J places a node or removes the placed one in front, and Shift+J picks the kind to place.
/// </summary>
[Service]
public sealed partial class MiningClient(
    IInput input,
    IPlayers players,
    IWeaponHits weaponHits,
    IEquippedWeapon equippedWeapon,
    ILocalProps localProps,
    IUserInterface ui,
    MiningClientRpc rpc,
    ILogger logger)
{
    private sealed record Flying(LocalPropHandle Piece, StoneChip Path, double ThrownAt, float Seconds);

    private const Key PlaceKey = Key.J;
    // The server answers a hit it counts with progress or a yield; an answer later than this is for no hit of ours
    private const double CountedWithinSeconds = 2;
    private const string QuarryHint = "This stone won't split cleanly. Quarries are marked on your map.";

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Dictionary<string, OreNodeView> _nodes = [];
    private readonly List<MarkedArea> _quarries = [];
    private readonly List<Flying> _flying = [];
    private IDisposable? _placeBind;
    private IDisposable? _cycleBind;
    private bool _askedForRules;
    private MiningRules? _rules;
    private int _placeKind;
    private bool _hinted;
    private double _reportedAt = double.NegativeInfinity;

    private double Now => _clock.Elapsed.TotalSeconds;

    private void Start()
    {
        _placeBind = input.RegisterKeyBind(PlaceKey, OnPlaceKey, KeySource.UnhandledByUi);
        _cycleBind = input.RegisterKeyBind(ModifierKeys.Shift, PlaceKey, OnCycleKey);
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
        _placeBind?.Dispose();
        _cycleBind?.Dispose();
        rpc.RulesReceived -= OnRules;
        rpc.QuarryAvailable -= OnQuarryAvailable;
        rpc.NodeAvailable -= OnNodeAvailable;
        rpc.NodeRemoved -= OnNodeRemoved;
        rpc.Progressed -= OnProgressed;
        rpc.Ended -= OnEnded;
        rpc.Struck -= OnStruck;
        weaponHits.Hit -= OnWeaponHit;
        foreach (var flying in _flying)
            localProps.Remove(flying.Piece);
        _flying.Clear();
    }

    private void Update()
    {
        FollowConnection();
        AnimatePieces();
    }

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
        logger.LogInformation("Jobs: mining by power attack; rock in a quarry is foliage with {Rock} but not {Excluded}{Landscape}; chips {Chips}; placement {Placement}.",
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
        if (_rules is null || hit.Attack != AttackKind.Power || !hit.WithWeapon)
            return;

        if (NodeAt(hit.Position) is { } node)
        {
            rpc.SendNodeHit(node.Id, hit.Position.X, hit.Position.Y, hit.Position.Z);
            _reportedAt = Now;
            logger.LogInformation("Jobs: power attack on {Node} at {At}.", node.Id, hit.Position);
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
            rpc.SendQuarryHit(quarry.Name, hit.Position.X, hit.Position.Y, hit.Position.Z, (byte)hit.Struck, hit.StruckFoliage?.Mesh ?? "");
            _reportedAt = Now;
            logger.LogInformation("Jobs: power attack on {Rock} in {Quarry} at {At}.", hit.StruckFoliage?.Mesh ?? "the ground", quarry.Name, hit.Position);
            return;
        }

        // A big rock out in the open: once, a word on where stone can be had
        if (!_hinted && hit.Struck == StruckSurface.Foliage)
        {
            _hinted = true;
            Say(QuarryHint, 5f);
        }
    }

    // The node whose middle the hit landed within reach of, nearest first
    private OreNodeView? NodeAt(Vector3 at)
        => _nodes.Values
            .Where(node => ChopGeometry.IsOnTrunk(at, node.Position, _rules!.HitRadius * node.Scale))
            .OrderBy(node => ChopGeometry.HorizontalDistance(at, node.Position))
            .Select(node => (OreNodeView?)node)
            .FirstOrDefault();

    private void OnProgressed(string what, int hits, int hitsNeeded)
    {
        Say($"{what}: {hits} / {hitsNeeded}", 1.5f);
        WearHammer();
    }

    private void OnEnded(MiningOutcome outcome, string message)
    {
        switch (outcome)
        {
            case MiningOutcome.Mined:
                WearHammer();
                break;
            case MiningOutcome.Refused:
            case MiningOutcome.Notice:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Not a mining outcome.");
        }
        Say(message, 4f);
    }

    // For a hit this client just reported and the server counted: it only counts one made with a warhammer
    private void WearHammer()
    {
        if (Now - _reportedAt > CountedWithinSeconds || _rules is not { WearPerHit: > 0f } rules)
            return;
        _reportedAt = double.NegativeInfinity;
        equippedWeapon.Wear(rules.WearPerHit);
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

        // The server's prop goes at once; pieces of it fly out where it stood and lie on the ground it stood on
        var center = position + new Vector3(0f, 0f, 40f * scale);
        for (var i = 0; i < rules.BurstPieces; i++)
        {
            var path = StoneChip.Burst(center, rules.BurstSpeed * (0.6f + Random.Shared.NextSingle() * 0.8f),
                (i + Random.Shared.NextSingle()) / rules.BurstPieces, Random.Shared.NextSingle(),
                (Random.Shared.NextSingle() - 0.5f) * 1080f, position.Z);
            Throw(path, rules.BurstScale * scale * (0.6f + Random.Shared.NextSingle() * 0.6f), rules.BurstSeconds);
        }
    }

    private void OnStruck(Vector3 at, Vector2 from, float groundZ)
    {
        if (_rules is not { ChipsPerHit: > 0, ChipAssets.Count: > 0 } rules || players.LocalMainCharacter is not { } character
            || ChopGeometry.HorizontalDistance(character.Position, at) > rules.ViewDistance)
            return;

        for (var i = 0; i < rules.ChipsPerHit; i++)
        {
            var path = StoneChip.Thrown(at, from, rules.ChipSpeed * (0.7f + Random.Shared.NextSingle() * 0.6f),
                Random.Shared.NextSingle(), Random.Shared.NextSingle(), (Random.Shared.NextSingle() - 0.5f) * 1440f, groundZ);
            Throw(path, rules.ChipScale, rules.ChipSeconds);
        }
    }

    private void Throw(StoneChip path, float scale, float seconds)
    {
        var asset = _rules!.ChipAssets[Random.Shared.Next(_rules.ChipAssets.Count)];
        var piece = localProps.Place(new Prop(asset, path.Start, Vector3.Zero, scale), collides: false);
        _flying.Add(new Flying(piece, path, Now, seconds));
    }

    private void AnimatePieces()
    {
        for (var i = _flying.Count - 1; i >= 0; i--)
        {
            var flying = _flying[i];
            var seconds = (float)(Now - flying.ThrownAt);
            if (seconds >= flying.Seconds)
            {
                localProps.Remove(flying.Piece);
                _flying.RemoveAt(i);
                continue;
            }
            localProps.Move(flying.Piece, flying.Path.PositionAt(seconds), flying.Path.RotationAt(seconds));
        }
    }

    private void OnPlaceKey()
    {
        if (_rules is not { PlacementAllowed: true })
            return;
        rpc.SendPlaceNode(_placeKind);
    }

    private void OnCycleKey()
    {
        if (_rules is not { PlacementAllowed: true, KindNames.Count: > 0 } rules)
            return;
        _placeKind = (_placeKind + 1) % rules.KindNames.Count;
        Say($"J places {rules.KindNames[_placeKind]} nodes.", 3f);
    }

    private void Say(string message, float seconds) => ui.ShowMessage(message, MessagePosition.TopLeft, seconds);
}
