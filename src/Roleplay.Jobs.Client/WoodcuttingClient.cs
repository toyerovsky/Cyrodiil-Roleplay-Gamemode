using System.Numerics;
using Microsoft.Extensions.Logging;
using OblivionMP.SDK;
using OblivionMP.SDK.Common.Foliage;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Events;
using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Client;

/// <summary>
/// Woodcutting is hitting: every attack the player lands on a tree, normal or power, goes to the server, which counts
/// its damage if a battle axe is in hand and fells the tree once it has taken its health, from anyone. There is no key
/// and no animation of this mod's own: the game's attacks are the swings. A normal attack always lands; a power attack
/// does more but often makes no impact on a trunk standing up. A counted hit wears the axe by its damage. With lumber
/// camps only, a tree outside every camp earns a hint, once, and is not reported. What happens is
/// <see cref="IWoodcutting"/>, for other mods to show.
/// </summary>
[Service]
public sealed partial class WoodcuttingClient(
    IPlayers players,
    IFoliage foliage,
    IWeaponHits weaponHits,
    IEquippedWeapon equippedWeapon,
    ChipThrower chips,
    JobsPanel panel,
    WoodcuttingClientRpc rpc,
    ILogger logger) : IWoodcutting
{
    private const string CampHint = "This tree is not for felling. Lumber camps are marked on your map.";

    private readonly List<MarkedArea> _camps = [];
    private bool _askedForRules;
    private WoodcuttingRules? _rules;
    private bool _hinted;
    private FoliageId _lastTree;

    public bool ShowMessages { get; set; } = true;

    public bool LumberCampsOnly => _rules?.LumberCampsOnly ?? false;

    public IReadOnlyList<MarkedArea> LumberCamps => _camps;

    public event Action<ChopHit>? HitCounted;
    public event Action<ChopEnd>? ChopEnded;
    public event Action<FelledTree>? TreeFelled;
    public event Action<FoliageId>? TreeGrewBack;

    private void Start()
    {
        rpc.RulesReceived += OnRules;
        rpc.LumberCampAvailable += OnLumberCamp;
        rpc.Progressed += OnProgressed;
        rpc.Ended += OnEnded;
        rpc.TreeGrewBack += OnTreeGrewBack;
        rpc.TreeStruck += OnTreeStruck;
        weaponHits.Hit += OnWeaponHit;
    }

    private void Stop()
    {
        rpc.RulesReceived -= OnRules;
        rpc.LumberCampAvailable -= OnLumberCamp;
        rpc.Progressed -= OnProgressed;
        rpc.Ended -= OnEnded;
        rpc.TreeGrewBack -= OnTreeGrewBack;
        rpc.TreeStruck -= OnTreeStruck;
        weaponHits.Hit -= OnWeaponHit;
    }

    private void Update() => FollowConnection();

    // The rules and camps come with each connection: a server can change them between restarts
    private void FollowConnection()
    {
        var connected = players.LocalMainCharacter is not null;
        if (connected == _askedForRules)
            return;

        _askedForRules = connected;
        _rules = null;
        _camps.Clear();
        if (connected)
            rpc.SendWoodcuttingReady();
    }

    private void OnRules(WoodcuttingRules rules)
    {
        _rules = rules;
        logger.LogInformation("Jobs: woodcutting by power attack; trees are meshes with {Trees} but not {Excluded}; {Where}.",
            TreeFilter.Join(rules.Trees.TreeMeshes), TreeFilter.Join(rules.Trees.ExcludedMeshes),
            rules.LumberCampsOnly ? "only in lumber camps" : "anywhere");
    }

    private void OnLumberCamp(MarkedArea camp)
    {
        _camps.RemoveAll(other => other.Name == camp.Name);
        _camps.Add(camp);
        logger.LogInformation("Jobs: lumber camp {Name}, {Radius:0} cm around {Center}.", camp.Name, camp.Radius, camp.Center);
    }

    private void OnWeaponHit(WeaponHit hit)
    {
        if (_rules is not { } rules || !hit.WithWeapon || JobAttackOf(hit.Attack) is not { } attack)
            return;
        if (TreeStruck(hit, rules) is not { } struck)
            return;
        var (tree, scale, how) = struck;

        if (rules.LumberCampsOnly && !_camps.Any(camp => camp.Holds(tree.Position)))
        {
            if (!_hinted)
            {
                _hinted = true;
                Say(PanelLineKind.Note, CampHint);
            }
            return;
        }

        rpc.SendTreeHit(tree.Mesh, tree.X, tree.Y, tree.Z, scale, hit.Position.X, hit.Position.Y, hit.Position.Z, (byte)attack);
        _lastTree = tree;
        logger.LogInformation("Jobs: {Attack} attack on {Tree} at {At} ({How}).", attack, tree, hit.Position, how);
    }

    private static JobAttack? JobAttackOf(AttackKind kind) => kind switch
    {
        AttackKind.Light => JobAttack.Normal,
        AttackKind.Power => JobAttack.Power,
        AttackKind.Arrow or AttackKind.Other => null,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not an attack kind."),
    };

    // The tree the hit landed on. The line traced through the point of contact names it when it meets the trunk, but a
    // trunk is thin and the line often passes beside it; then the nearest standing tree whose trunk the point is on
    // counts, as the server judges it.
    private (FoliageId Tree, float Scale, string How)? TreeStruck(WeaponHit hit, WoodcuttingRules rules)
    {
        if (hit.Struck == StruckSurface.Foliage && hit.StruckFoliage is { } struck && rules.Trees.IsTree(struck.Mesh))
        {
            if (foliage.IsHidden(struck))
                return null;
            return (struck, foliage.TryGetTransform(struck, out var standing) ? standing.Scale : 1f, "struck");
        }

        var near = foliage.FindNear(hit.Position, TreeSearchRadius, rules.Trees.SearchHint, 8)
            .Where(found => rules.Trees.IsTree(found.Id.Mesh) && !foliage.IsHidden(found.Id))
            .ToList();
        foreach (var found in near.OrderBy(found => ChopGeometry.HorizontalDistance(hit.Position, found.Id.Position)))
        {
            if (ChopGeometry.IsOnTrunk(hit.Position, found.Id.Position, TrunkRadius(rules, found.Scale)))
                return (found.Id, found.Scale, $"on its trunk, struck {hit.Struck}");
        }

        if (near.Count > 0)
        {
            var nearest = near.MinBy(found => ChopGeometry.HorizontalDistance(hit.Position, found.Id.Position));
            logger.LogInformation("Jobs: power attack at {At} struck {Struck}, {Distance:0} cm from the trunk of {Tree}: missed.",
                hit.Position, hit.Struck, ChopGeometry.HorizontalDistance(hit.Position, nearest.Id.Position), nearest.Id);
        }
        return null;
    }

    // How far from the trunk's middle a hit counts; the server allows the same, plus a little for rounding
    private static float TrunkRadius(WoodcuttingRules rules, float scale) => rules.HitRadius * MathF.Max(1f, scale);

    // How far from the point of contact to look for a trunk: the swing lands up to TrunkAbove over its base
    private const float TreeSearchRadius = ChopGeometry.TrunkAbove + 500f;

    private void OnProgressed(ChopProgressNews news)
    {
        if (ShowMessages)
            panel.Progress(news.Kind.Length > 0 ? news.Kind : "Tree", news.Damage, news.Health);
        if (news.Wear > 0f && float.IsFinite(news.Wear))
            equippedWeapon.Wear(news.Wear);
        Raise(nameof(HitCounted), () => HitCounted?.Invoke(new ChopHit(_lastTree, news.Kind, news.Damage, news.Health)));
    }

    private void OnEnded(ChopOutcome outcome, string message, IReadOnlyList<string> gains)
    {
        var reason = outcome switch
        {
            ChopOutcome.Felled => ChopEndReason.Felled,
            ChopOutcome.Refused => ChopEndReason.Refused,
            ChopOutcome.Glanced => ChopEndReason.Glanced,
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Not a chop outcome."),
        };
        if (ShowMessages && reason == ChopEndReason.Felled)
            panel.Outcome(gains, message);
        else
            Say(reason == ChopEndReason.Glanced ? PanelLineKind.Note : PanelLineKind.Refusal, message);
        logger.LogInformation("Jobs: chop {Outcome}: {Message}", outcome, message);
        Raise(nameof(ChopEnded), () => ChopEnded?.Invoke(new ChopEnd(reason, message, gains)));
    }

    /// <summary>A tree came down near this player: tells the mods that listen through <see cref="IWoodcutting"/>.</summary>
    [EventHandler(Stage.Observe)]
    public void OnTreeFelled(in TreeFelledEvent felled, EventContext context)
    {
        var feller = JobEvents.PlayerOf(felled.Feller);
        var news = new FelledTree(felled.Tree(), felled.Kind ?? "", felled.Scale, feller,
            players.LocalPlayer is { } me && feller == me.PlayerId);
        Raise(nameof(TreeFelled), () => TreeFelled?.Invoke(news));
    }

    // A counted hit, anyone's: bark flies off the trunk, back towards the chopper, for the players near
    private void OnTreeStruck(Vector3 at, Vector2 from, float groundZ)
    {
        if (_rules is not { ChipsPerHit: > 0, ChipAssets.Count: > 0 } rules || players.LocalMainCharacter is not { } character
            || ChopGeometry.HorizontalDistance(character.Position, at) > rules.ViewDistance)
            return;
        chips.ThrowOffHit(rules.ChipAssets, rules.ChipsPerHit, at, from, groundZ, rules.ChipSpeed, rules.ChipScale, rules.ChipSeconds);
    }

    private void OnTreeGrewBack(FoliageId tree)
        => Raise(nameof(TreeGrewBack), () => TreeGrewBack?.Invoke(tree));

    private void Say(PanelLineKind kind, string message)
    {
        if (ShowMessages)
            panel.Say(kind, message);
    }

    // Another mod's handler failing must not stop woodcutting
    private void Raise(string what, Action raise)
    {
        try
        {
            raise();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Jobs: a mod's {Event} handler failed.", what);
        }
    }
}
