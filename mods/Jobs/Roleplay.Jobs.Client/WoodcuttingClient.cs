using System.Diagnostics;
using System.Numerics;
using Microsoft.Extensions.Logging;
using OblivionMp.Sdk;
using OblivionMp.Sdk.Common.Foliage;
using OblivionMpCSharpMod;
using ReadyM.Api.Idents;
using ReadyM.SDK.Attributes;
using OblivionMpCSharpMod.Input;
using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Client;

/// <summary>
/// G at a tree asks the server to chop it. The character then swings at the server's pace; each swing whose axe lands
/// on the trunk goes to the server, which fells the tree at the last hit it needs. Walking away, pressing G again or
/// running out of swings stops. What it is doing is <see cref="IWoodcutting"/>, for other mods to show.
/// </summary>
[Service]
public sealed partial class WoodcuttingClient(
    IInput input,
    IPlayers players,
    IFoliage foliage,
    IAnimations animations,
    IWeaponHits weaponHits,
    IEquippedWeapon equippedWeapon,
    IUserInterface ui,
    WoodcuttingClientRpc rpc,
    ILogger logger) : IWoodcutting
{
    private const Key ChopKey = Key.G;
    // A press the stop bind took is not also a start: the two binds hear the same press, in either order
    private const double KeyDebounceSeconds = 0.4;
    // How far the character may drift from where the chop started, in centimeters
    private const float MoveTolerance = 120f;
    private const float StopBlendSeconds = 0.25f;
    // What another client may have this one play on its character: the game's own animations, nothing else
    private const string AnimationRoot = "/Game/Art/Animation/";

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private IDisposable? _bind;
    private IDisposable? _stopBind;
    private double _stoppedByKeyAt = double.NegativeInfinity;
    private bool _askedForRules;
    private TreeFilter? _filter;
    private float _reach;
    private float _facingDegrees;

    private FoliageId _tree;
    private float _treeScale;
    private ChopOrders _orders;
    private int _swingsDone;
    private int _hitsDone;
    private bool _swingLanded;
    private double _nextSwingAt;
    private Vector3 _startedAt;

    public ChopPhase Phase { get; private set; } = ChopPhase.Idle;

    public Chop? Current => Phase == ChopPhase.Swinging
        ? new Chop(_tree, _orders.Kind, _orders.WoodTier, _orders.AxeTier, _treeScale, _hitsDone, _orders.HitsNeeded, _swingsDone, _orders.MaxSwings)
        : null;

    public bool ShowMessages { get; set; } = true;

    public event Action<Chop>? ChopStarted;
    public event Action<Chop>? HitLanded;
    public event Action<ChopEnd>? ChopEnded;
    public event Action<FelledTree>? TreeFelled;
    public event Action<FoliageId>? TreeGrewBack;

    private double Now => _clock.Elapsed.TotalSeconds;

    private void Start()
    {
        _bind = input.RegisterKeyBind(ChopKey, OnChopKey, KeySource.UnhandledByUi);
        // Mid swing the game takes the press for itself, so the unhandled one never comes: stopping polls the keyboard
        _stopBind = input.RegisterKeyBind(ChopKey, OnStopKey);
        rpc.RulesReceived += OnRules;
        rpc.Started += OnStarted;
        rpc.Progressed += OnProgressed;
        rpc.Ended += OnEnded;
        rpc.TreeFelled += OnTreeFelled;
        rpc.TreeGrewBack += OnTreeGrewBack;
        rpc.OtherSwung += OnOtherSwung;
        weaponHits.Hit += OnWeaponHit;
    }

    private void Stop()
    {
        _bind?.Dispose();
        _stopBind?.Dispose();
        rpc.RulesReceived -= OnRules;
        rpc.Started -= OnStarted;
        rpc.Progressed -= OnProgressed;
        rpc.Ended -= OnEnded;
        rpc.TreeFelled -= OnTreeFelled;
        rpc.TreeGrewBack -= OnTreeGrewBack;
        rpc.OtherSwung -= OnOtherSwung;
        weaponHits.Hit -= OnWeaponHit;
    }

    private void Update()
    {
        FollowConnection();
        if (Phase == ChopPhase.Swinging)
            Swing();
    }

    // The rules come with each connection: a server can change them between restarts
    private void FollowConnection()
    {
        var connected = players.LocalMainCharacter is not null;
        if (connected == _askedForRules)
            return;

        _askedForRules = connected;
        _filter = null;
        Phase = ChopPhase.Idle;
        if (connected)
            rpc.SendWoodcuttingReady();
    }

    private void OnChopKey()
    {
        if (Now - _stoppedByKeyAt < KeyDebounceSeconds)
            return;

        switch (Phase)
        {
            case ChopPhase.Idle:
                TryStart();
                break;
            case ChopPhase.Swinging:
            case ChopPhase.Asking:
                break; // stopping is OnStopKey's; asking waits on the server, which answers either way
            default:
                throw new ArgumentOutOfRangeException(nameof(Phase), Phase, "Not a chop phase.");
        }
    }

    private void OnStopKey()
    {
        if (Phase != ChopPhase.Swinging)
            return;
        Cancel("You stop chopping.");
        _stoppedByKeyAt = Now;
    }

    private void TryStart()
    {
        if (_filter is null || players.LocalMainCharacter is not { } character)
        {
            Say("Woodcutting is not ready yet.", 3f);
            return;
        }

        var trees = foliage.FindNear(character.Position, _reach, _filter.SearchHint, 16)
            .Where(found => _filter.IsTree(found.Id.Mesh) && !foliage.IsHidden(found.Id))
            .ToList();
        if (trees.Count == 0)
        {
            Say("There is no tree within reach.", 3f);
            return;
        }

        // Nearest first, so the first one in front is the one the swing would hit
        var tree = trees.FirstOrDefault(found =>
            ChopGeometry.DegreesOffFacing(character.Position, character.Rotation, found.Id.Position) <= _facingDegrees);
        if (tree.Id.Mesh is null)
        {
            Say("Face the tree you want to chop.", 3f);
            return;
        }

        _tree = tree.Id;
        _treeScale = tree.Scale;
        Phase = ChopPhase.Asking;
        rpc.SendStartChop(_tree.Mesh, _tree.X, _tree.Y, _tree.Z, tree.Scale);
        logger.LogInformation("Jobs: asked to chop {Tree}, {Distance:0} cm away.", _tree, tree.Distance);
    }

    private void OnRules(TreeFilter filter, float reach, float facingDegrees, TreeFall? fall)
    {
        _filter = filter;
        _reach = reach;
        _facingDegrees = facingDegrees;
        logger.LogInformation("Jobs: trees are meshes with {Trees} but not {Excluded}, within {Reach} cm and {Facing} degrees of facing.",
            TreeFilter.Join(filter.TreeMeshes), TreeFilter.Join(filter.ExcludedMeshes), reach, facingDegrees);
    }

    private void OnStarted(ChopOrders orders)
    {
        if (Phase != ChopPhase.Asking || players.LocalMainCharacter is not { } character)
        {
            rpc.SendCancelChop();
            return;
        }

        _orders = orders;
        _swingsDone = 0;
        _hitsDone = 0;
        _swingLanded = false;
        _nextSwingAt = Now;
        _startedAt = character.Position;
        Phase = ChopPhase.Swinging;
        Raise(nameof(ChopStarted), () => ChopStarted?.Invoke(Current!));
    }

    private void Swing()
    {
        if (players.LocalMainCharacter is not { } character)
        {
            Cancel("You stop chopping.");
            return;
        }

        if (ChopGeometry.HorizontalDistance(character.Position, _startedAt) > MoveTolerance)
        {
            Cancel("You moved away from the tree.");
            return;
        }

        if (Now < _nextSwingAt)
            return;

        // The last swing has had its full time to land
        if (_swingsDone >= _orders.MaxSwings)
        {
            Cancel("Your swings keep missing the trunk.");
            return;
        }

        var animation = _orders.FirstPersonAnimation.Length > 0 ? _orders.FirstPersonAnimation : null;
        var seconds = animations.Play(_orders.Animation, animation, _orders.PlayRate);
        if (seconds <= 0f)
            logger.LogWarning("Jobs: the swing {Animation} did not play.", _orders.Animation);
        else if (_swingsDone == 0)
            logger.LogInformation("Jobs: the swing runs {Seconds:0.00} s, one starts every {Interval:0.00} s.", seconds, _orders.SwingSeconds);
        if (seconds > 0f && !IsMontage(_orders.Animation))
            rpc.SendSwung(_orders.Animation, _orders.PlayRate);

        _swingsDone++;
        _swingLanded = false;
        _nextSwingAt += _orders.SwingSeconds;
    }

    private void OnWeaponHit(WeaponHit hit)
    {
        if (Phase != ChopPhase.Swinging || _swingLanded)
            return;

        if (!ChopGeometry.IsOnTrunk(hit.Position, _tree.Position, _orders.HitRadius))
        {
            logger.LogInformation("Jobs: swing {Swing} hit {At}, {Distance:0} cm from the trunk of {Tree}: missed.",
                _swingsDone, hit.Position, ChopGeometry.HorizontalDistance(hit.Position, _tree.Position), _tree);
            return;
        }

        _swingLanded = true;
        rpc.SendChopHit(hit.Position.X, hit.Position.Y, hit.Position.Z);
        logger.LogInformation("Jobs: swing {Swing} landed on {Tree} at {At}.", _swingsDone, _tree, hit.Position);
    }

    private void OnProgressed(int hits, int hitsNeeded)
    {
        Say($"Chop {hits}/{hitsNeeded}", 1.5f);
        if (Phase != ChopPhase.Swinging || hits == 0)
            return;

        _hitsDone = hits;
        WearAxe();
        Raise(nameof(HitLanded), () => HitLanded?.Invoke(Current!));
    }

    private void OnEnded(ChopOutcome outcome, string message)
    {
        // A felled tree comes down mid-swing; the swing plays out
        if (outcome == ChopOutcome.Refused && Phase == ChopPhase.Swinging)
            animations.Stop(StopBlendSeconds);
        if (outcome == ChopOutcome.Felled && Phase == ChopPhase.Swinging)
        {
            _hitsDone = _orders.HitsNeeded;
            WearAxe();
        }
        Phase = ChopPhase.Idle;
        Say(message, 4f);
        logger.LogInformation("Jobs: chop of {Tree} ended, {Outcome}: {Message}", _tree, outcome, message);

        var reason = outcome switch
        {
            ChopOutcome.Felled => ChopEndReason.Felled,
            ChopOutcome.Refused => ChopEndReason.Refused,
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Not a chop outcome."),
        };
        Raise(nameof(ChopEnded), () => ChopEnded?.Invoke(new ChopEnd(_tree, reason, message)));
    }

    private void OnTreeFelled(FoliageId tree, string kind, float scale, bool byYou, float fallYaw)
        => Raise(nameof(TreeFelled), () => TreeFelled?.Invoke(new FelledTree(tree, kind, scale, byYou)));

    private void OnOtherSwung(PlayerId sender, string animation, float playRate)
    {
        if (!animation.StartsWith(AnimationRoot, StringComparison.OrdinalIgnoreCase) || !float.IsFinite(playRate) || playRate <= 0f)
        {
            logger.LogWarning("Jobs: {Player} sent a swing of {Animation} at {Rate}, which is not one of the game's. Ignored.", sender, animation, playRate);
            return;
        }

        foreach (var character in players.AreaPlayers)
        {
            if (character.PlayerId != sender)
                continue;
            if (animations.PlayOn(character, animation, playRate) <= 0f)
                logger.LogWarning("Jobs: {Player}'s swing {Animation} did not play on their character here.", sender, animation);
            return;
        }
    }

    // A montage replicates by itself; an animation sequence does not. Their names tell them apart: AM_ against A_
    private static bool IsMontage(string animationPath)
        => animationPath[(animationPath.LastIndexOf('/') + 1)..].StartsWith("AM_", StringComparison.OrdinalIgnoreCase);

    private void OnTreeGrewBack(FoliageId tree)
        => Raise(nameof(TreeGrewBack), () => TreeGrewBack?.Invoke(tree));

    private void Cancel(string message)
    {
        animations.Stop(StopBlendSeconds);
        rpc.SendCancelChop();
        Phase = ChopPhase.Idle;
        Say(message, 3f);
        Raise(nameof(ChopEnded), () => ChopEnded?.Invoke(new ChopEnd(_tree, ChopEndReason.Stopped, message)));
    }

    // Only with an axe the server counts: a server that needs none lets the player chop with whatever is in hand
    private void WearAxe()
    {
        if (_orders.AxeTier is not null && _orders.WearPerHit > 0f)
            equippedWeapon.Wear(_orders.WearPerHit);
    }

    private void Say(string message, float seconds)
    {
        if (ShowMessages)
            ui.ShowMessage(message, MessagePosition.TopLeft, seconds);
    }

    // Another mod's handler failing must not stop the chop
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
