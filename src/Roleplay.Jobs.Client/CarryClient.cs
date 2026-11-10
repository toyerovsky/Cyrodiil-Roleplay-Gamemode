using System.Diagnostics;
using System.Numerics;
using Microsoft.Extensions.Logging;
using OblivionMP.SDK;
using OblivionMP.SDK.Common.Archetypes;
using OblivionMP.SDK.Common.Props;
using OblivionMpCSharpMod.Input;
using ReadyM.SDK.Attributes;

namespace Roleplay.Jobs.Client;

/// <summary>
/// Carrying on this client: the carry key asks the server to pick up what is in front or put down what is in hand, and
/// every carrier near, this player too, gets a copy of what they carry hung on their hand socket, since a prop on a
/// socket is local. What is put down or placed is swept from in front of the player's chest to its spot and let fall
/// onto whatever is below (<see cref="ILocalProps.FindRest"/>), and the server is told where it rests. A carrier holds their arms out with the hold animation, played again before it ends, and what they
/// carry takes its standing or its moving fit by how fast they go. With placement allowed, Shift and the key place the
/// next kind in front, and the numpad nudges the fit in hand for the pose the player is in (Ctrl: offset, Alt:
/// rotation), logging it to copy into the server's config.
/// </summary>
[Service]
public sealed partial class CarryClient(
    IInput input,
    IPlayers players,
    ILocalProps localProps,
    IAnimations animations,
    JobsPanel panel,
    CarryClientRpc rpc,
    ILogger logger)
{
    private sealed record HeldProp(int KindIndex, LocalPropHandle? Prop, CarryPose Pose, double RetryAt, Vector3 LastPosition,
        double LastSeenAt, double AnimateAt);

    private sealed record Nudge(Key Key, ModifierKeys Modifiers, Vector3 Offset, Vector3 Rotation);

    private const double RetryEverySeconds = 1;
    // Positions of other players arrive a few times a second, so speed is judged over this long, not frame to frame
    private const double CheckEverySeconds = 0.25;
    // The hold animation is played again this far into it, before it blends out and drops the arms
    private const double ReplayAt = 0.85;
    private const float StopBlendSeconds = 0.25f;
    private const float NudgeCentimeters = 2f;
    private const float NudgeDegrees = 10f;

    private static readonly Nudge[] Nudges =
    [
        new(Key.NUM_FOUR, ModifierKeys.Control, new Vector3(-NudgeCentimeters, 0, 0), Vector3.Zero),
        new(Key.NUM_SIX, ModifierKeys.Control, new Vector3(NudgeCentimeters, 0, 0), Vector3.Zero),
        new(Key.NUM_SEVEN, ModifierKeys.Control, new Vector3(0, -NudgeCentimeters, 0), Vector3.Zero),
        new(Key.NUM_NINE, ModifierKeys.Control, new Vector3(0, NudgeCentimeters, 0), Vector3.Zero),
        new(Key.NUM_TWO, ModifierKeys.Control, new Vector3(0, 0, -NudgeCentimeters), Vector3.Zero),
        new(Key.NUM_EIGHT, ModifierKeys.Control, new Vector3(0, 0, NudgeCentimeters), Vector3.Zero),
        new(Key.NUM_TWO, ModifierKeys.Alt, Vector3.Zero, new Vector3(-NudgeDegrees, 0, 0)),
        new(Key.NUM_EIGHT, ModifierKeys.Alt, Vector3.Zero, new Vector3(NudgeDegrees, 0, 0)),
        new(Key.NUM_FOUR, ModifierKeys.Alt, Vector3.Zero, new Vector3(0, -NudgeDegrees, 0)),
        new(Key.NUM_SIX, ModifierKeys.Alt, Vector3.Zero, new Vector3(0, NudgeDegrees, 0)),
        new(Key.NUM_SEVEN, ModifierKeys.Alt, Vector3.Zero, new Vector3(0, 0, -NudgeDegrees)),
        new(Key.NUM_NINE, ModifierKeys.Alt, Vector3.Zero, new Vector3(0, 0, NudgeDegrees)),
    ];

    private readonly Dictionary<int, CarryKindView> _kinds = [];
    private readonly Dictionary<int, HeldProp> _held = [];
    private readonly List<IDisposable> _binds = [];
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private CarryRules? _rules;
    private bool _askedForRules;
    private int _placeKind;
    private double _nextCheck;

    private double Now => _clock.Elapsed.TotalSeconds;

    private void Start()
    {
        rpc.RulesReceived += OnRules;
        rpc.KindReceived += OnKind;
        rpc.CarryingStarted += OnCarryingStarted;
        rpc.CarryingStopped += OnCarryingStopped;
        rpc.Refused += OnRefused;
    }

    private void Stop()
    {
        rpc.RulesReceived -= OnRules;
        rpc.KindReceived -= OnKind;
        rpc.CarryingStarted -= OnCarryingStarted;
        rpc.CarryingStopped -= OnCarryingStopped;
        rpc.Refused -= OnRefused;
        Forget();
    }

    private void Update()
    {
        FollowConnection();
        if (Now < _nextCheck)
            return;
        _nextCheck = Now + CheckEverySeconds;
        foreach (var player in _held.Keys.ToList())
            Hold(player);
    }

    // The rules, kinds and carriers come with each connection: what an earlier server sent is dropped
    private void FollowConnection()
    {
        var connected = players.LocalMainCharacter is not null;
        if (connected == _askedForRules)
            return;

        _askedForRules = connected;
        Forget();
        if (connected)
            rpc.SendCarryReady();
    }

    private void Forget()
    {
        foreach (var bind in _binds)
            bind.Dispose();
        _binds.Clear();
        foreach (var held in _held.Values)
            if (held.Prop is { } prop)
                localProps.Remove(prop);
        _held.Clear();
        _kinds.Clear();
        _rules = null;
    }

    private void OnRules(CarryRules rules)
    {
        _rules = rules;
        foreach (var bind in _binds)
            bind.Dispose();
        _binds.Clear();
        // Polled: the game's own UI takes U before an UnhandledByUi bind sees it ("handled by the UI before its bind
        // could see it"), so the key is read from the keyboard and ignored while a text field or menu has it
        _binds.Add(input.RegisterKeyBind(rules.Key, WhenPlaying(OnCarryKey), KeySource.Polled));
        if (rules.PlacementAllowed)
        {
            _binds.Add(input.RegisterKeyBind(ModifierKeys.Shift, rules.Key, WhenPlaying(OnPlaceKey), KeySource.Polled));
            foreach (var nudge in Nudges)
                _binds.Add(input.RegisterKeyBind(nudge.Modifiers, nudge.Key, WhenPlaying(() => OnNudge(nudge)), KeySource.Polled));
        }
        logger.LogInformation("Jobs: carrying on {Key}; placement {Placement}; holding {Animation}.", rules.Key,
            rules.PlacementAllowed ? "on" : "off", rules.HoldAnimation.Length > 0 ? rules.HoldAnimation : "nothing");
    }

    private Action WhenPlaying(Action action) => () =>
    {
        if (input.CanApplyInput())
            action();
    };

    private void OnKind(CarryKindView kind) => _kinds[kind.Index] = kind;

    private void OnCarryingStarted(int player, int kindIndex)
    {
        if (_held.Remove(player, out var before) && before.Prop is { } prop)
            localProps.Remove(prop);
        _held[player] = new HeldProp(kindIndex, null, CarryPose.Standing, 0, Vector3.Zero, 0, 0);
        Hold(player);
    }

    private void OnCarryingStopped(int player)
    {
        if (_held.Remove(player, out var held) && held.Prop is { } prop)
            localProps.Remove(prop);
        // Another player's arms come down when the animation runs out: only this player's can be stopped
        if (IsLocal(player) && _rules is { HoldAnimation.Length: > 0 })
            animations.Stop(StopBlendSeconds);
    }

    private void OnRefused(string message) => panel.Say(PanelLineKind.Refusal, message);

    private void OnCarryKey()
    {
        if (players.LocalMainCharacter is { } local && _held.TryGetValue(local.PlayerId.RawValue, out var held)
            && _kinds.TryGetValue(held.KindIndex, out var kind))
        {
            if (RestFor(local, kind) is { } rest)
                rpc.SendPutDown(rest.X, rest.Y, rest.Z, local.Rotation);
            return;
        }
        rpc.SendCarryKey();
    }

    private void OnPlaceKey()
    {
        if (_kinds.Count == 0 || players.LocalMainCharacter is not { } local)
            return;
        var index = _placeKind % _kinds.Count;
        if (!_kinds.TryGetValue(index, out var kind))
            return;
        if (RestFor(local, kind) is not { } rest)
            return;
        rpc.SendPlaceCarryable(index, rest.X, rest.Y, rest.Z, local.Rotation);
        panel.Say(PanelLineKind.Note, $"Placed a {kind.Name}.");
        _placeKind = (_placeKind + 1) % _kinds.Count;
    }

    // Where the kind comes to rest put down in front of the player, or null with the reason on the panel
    private Vector3? RestFor(MainCharacter local, CarryKindView kind)
    {
        if (_rules is not { } rules)
            return null;
        var yaw = local.Rotation;
        var from = local.Position + new Vector3(0, 0, rules.CarryHeight);
        var radians = yaw * MathF.PI / 180f;
        var to = from + new Vector3(MathF.Cos(radians) * rules.PutDownAhead, MathF.Sin(radians) * rules.PutDownAhead, 0);
        var found = localProps.FindRest(new Prop(kind.Asset, to, new Vector3(0, yaw, 0), kind.Scale), from, rules.MaxDrop, out var rest);
        switch (found)
        {
            case PropRest.Rests:
                return rest;
            case PropRest.Blocked:
                panel.Say(PanelLineKind.Refusal, "There is no room for it there.");
                return null;
            case PropRest.NoFloor:
                panel.Say(PanelLineKind.Refusal, "There is nothing there to put it on.");
                return null;
            case PropRest.Unknown:
                logger.LogWarning("Jobs: could not find where the {Kind} ({Asset}) would rest.", kind.Name, kind.Asset);
                panel.Say(PanelLineKind.Refusal, "It cannot be put down here.");
                return null;
            default:
                throw new ArgumentOutOfRangeException(nameof(found), found, "Not a prop rest.");
        }
    }

    // Keeps one carrier's arms out and what they carry in hand, fitted to how they move, while their character is near
    // enough to be drawn, and takes it off while it is not
    private void Hold(int player)
    {
        var held = _held[player];
        if (CharacterOf(player) is not { } carrier)
        {
            if (held.Prop is { } gone)
                localProps.Remove(gone);
            _held[player] = held with { Prop = null, LastSeenAt = 0 };
            return;
        }
        if (!_kinds.TryGetValue(held.KindIndex, out var kind))
        {
            logger.LogWarning("Jobs: {Player} carries kind {Kind}, which the server never described.", player, held.KindIndex);
            _held[player] = held with { RetryAt = double.MaxValue };
            return;
        }

        held = Animate(player, carrier, Move(held, carrier));
        if (held.Prop is { } hung)
        {
            if (held.Pose != _held[player].Pose && !Attach(hung, carrier, kind, held.Pose))
            {
                localProps.Remove(hung);
                held = held with { Prop = null, RetryAt = Now + RetryEverySeconds };
            }
        }
        else if (Now >= held.RetryAt)
        {
            var prop = localProps.Place(new Prop(kind.Asset, carrier.Position, Vector3.Zero, kind.Scale), collides: false);
            if (Attach(prop, carrier, kind, held.Pose))
            {
                held = held with { Prop = prop };
            }
            else
            {
                localProps.Remove(prop);
                held = held with { RetryAt = Now + RetryEverySeconds };
            }
        }
        _held[player] = held;
    }

    // Moving or standing, by how far the carrier went since the last look
    private HeldProp Move(HeldProp held, MainCharacter carrier)
    {
        var position = carrier.Position;
        var pose = held.Pose;
        if (held.LastSeenAt > 0 && _rules is { } rules)
        {
            var seconds = Now - held.LastSeenAt;
            var distance = Vector2.Distance(new Vector2(position.X, position.Y), new Vector2(held.LastPosition.X, held.LastPosition.Y));
            pose = seconds > 0 && distance / seconds > rules.MovingAbove ? CarryPose.Moving : CarryPose.Standing;
        }
        return held with { Pose = pose, LastPosition = position, LastSeenAt = Now };
    }

    private HeldProp Animate(int player, MainCharacter carrier, HeldProp held)
    {
        if (_rules is not { HoldAnimation.Length: > 0 } rules || Now < held.AnimateAt)
            return held;

        var seconds = IsLocal(player)
            ? animations.Play(rules.HoldAnimation, rules.HoldAnimationFirstPerson.Length > 0 ? rules.HoldAnimationFirstPerson : null, 1f)
            : animations.PlayOn(carrier, rules.HoldAnimation, 1f);
        return held with { AnimateAt = Now + (seconds > 0 ? seconds * ReplayAt : RetryEverySeconds) };
    }

    private bool Attach(LocalPropHandle prop, MainCharacter carrier, CarryKindView kind, CarryPose pose)
    {
        var fit = kind.FitFor(pose);
        return localProps.AttachTo(prop, carrier, CharacterMesh.ThirdPerson, kind.Socket, fit.Offset, fit.Rotation);
    }

    private bool IsLocal(int player) => players.LocalMainCharacter is { } local && local.PlayerId.RawValue == player;

    private MainCharacter? CharacterOf(int player)
    {
        foreach (var character in players.AreaPlayers)
            if (character.PlayerId.RawValue == player)
                return character;
        return players.LocalMainCharacter is { } local && local.PlayerId.RawValue == player ? local : null;
    }

    private void OnNudge(Nudge nudge)
    {
        if (players.LocalMainCharacter is not { } local || !_held.TryGetValue(local.PlayerId.RawValue, out var held)
            || !_kinds.TryGetValue(held.KindIndex, out var kind))
            return;

        var fit = kind.FitFor(held.Pose);
        var nudged = fit with { Offset = fit.Offset + nudge.Offset, Rotation = fit.Rotation + nudge.Rotation };
        _kinds[held.KindIndex] = kind.WithFit(held.Pose, nudged);
        if (held.Prop is { } prop && !Attach(prop, local, _kinds[held.KindIndex], held.Pose))
        {
            localProps.Remove(prop);
            _held[local.PlayerId.RawValue] = held with { Prop = null, RetryAt = 0 };
        }

        var key = held.Pose switch
        {
            CarryPose.Standing => "standing",
            CarryPose.Moving => "moving",
        };
        logger.LogInformation("Jobs: {Kind} in hand, \"{Pose}\": {Fit}", kind.Name, key, nudged.ToConfig());
        panel.Say(PanelLineKind.Note, $"{kind.Name} {key}: {nudged.ToConfig()}");
    }
}
