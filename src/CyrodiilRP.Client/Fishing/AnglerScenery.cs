using System.Numerics;
using CyrodiilRP.Common.Fishing;
using Microsoft.Extensions.Logging;
using OblivionMP.SDK;
using OblivionMP.SDK.Common.Archetypes;
using OblivionMP.SDK.Common.Props;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client.Fishing;

// Every angler this player sees, themselves included: the rod in the hand, the float on the water, the splashes seen and
// heard here. The game has no fishing rod, so the rod is its plain staff's mesh, and the float a quill.
[Service]
internal sealed partial class AnglerScenery(
    RpcHandlers rpc,
    IPlayers players,
    ILocalProps props,
    IAnimations animations,
    IVisualEffects effects,
    ISounds sounds,
    ILogger logger)
{
    private const string RodMesh = "/Game/Art/Equipment/staffs/SM_Staff01.SM_Staff01";
    private const string HandSocket = "Weapon_Socket";
    private const string FloatMesh = "/Game/Art/Environment/clutter/SM_quill01.SM_quill01";
    private const float FloatScale = 2f;
    // Upright enough to be seen on the water.
    private const float FloatPitch = 70f;

    // The staff's hold, on the upper body, played again just before it ends.
    private const string Hold = "/Game/Art/Animation/Humanoid/ThirdPerson/Staff/Locomotion/Normal/A_Humanoid_Staff_Idle.A_Humanoid_Staff_Idle";
    private const float HoldOverlap = 0.2f;
    private const float HoldRetrySeconds = 1f;
    private const float HoldBlendOutSeconds = 0.3f;

    // A share of the way to its target the float glides a second, and a jump it takes at once (a new cast).
    private const float FloatGlide = 12f;
    private const float FloatJumps = 300f;

    private const string SplashSmall = "/Game/Art/FX/Environment/NS_Static_WaterSplash_01.NS_Static_WaterSplash_01";
    private const string SplashBig = "/Game/Art/FX/Environment/NS_Static_WaterSplash_03.NS_Static_WaterSplash_03";
    private const string SplashSound = "/Game/WwiseAudio/Character/Foley/char_fol_water_splash.char_fol_water_splash";
    private const string ThrashSound = "/Game/WwiseAudio/Character/Foley/char_fol_swim.char_fol_swim";
    // The game's splashes keep running unseen after they show, so each is taken away after this.
    private const float SplashSeconds = 4f;
    private const float SmallSplash = 0.5f;
    private const float FullSplash = 1f;

    private readonly Dictionary<int, AnglerScene> _anglers = [];

    private void Start()
    {
        rpc.AnglerShown += OnShown;
        rpc.FishingFloatMoved += OnFloatMoved;
        rpc.FishingFloatGone += OnFloatGone;
        rpc.FishingSplashed += OnSplashed;
    }

    private void Stop()
    {
        rpc.AnglerShown -= OnShown;
        rpc.FishingFloatMoved -= OnFloatMoved;
        rpc.FishingFloatGone -= OnFloatGone;
        rpc.FishingSplashed -= OnSplashed;
        foreach (var angler in _anglers.Keys.ToList())
            Forget(angler);
    }

    // Where the own float is drawn now, for the angler's camera and page.
    public Vector3? FindFloat(int angler) => _anglers.TryGetValue(angler, out var scene) ? scene.FloatDrawn : null;

    private void OnShown(int angler, bool fishing)
    {
        if (!fishing)
            Forget(angler);
        else if (!_anglers.ContainsKey(angler))
            _anglers[angler] = new AnglerScene(Rod: null, Float: null, FloatDrawn: null, FloatTarget: null, HoldAgainAt: 0f);
    }

    private void OnFloatMoved(int angler, Vector3 at)
    {
        if (!_anglers.TryGetValue(angler, out var scene))
            return;

        var drawn = scene.FloatDrawn is { } before && Vector3.Distance(before, at) < FloatJumps ? before : at;
        var handle = scene.Float ?? props.Place(new Prop(FloatMesh, drawn, new Vector3(FloatPitch, 0f, 0f), FloatScale), collides: false);
        props.Move(handle, drawn, new Vector3(FloatPitch, 0f, 0f));
        _anglers[angler] = scene with { Float = handle, FloatDrawn = drawn, FloatTarget = at };
    }

    private void OnFloatGone(int angler)
    {
        if (!_anglers.TryGetValue(angler, out var scene) || scene.Float is not { } handle)
            return;

        props.Remove(handle);
        _anglers[angler] = scene with { Float = null, FloatDrawn = null, FloatTarget = null };
    }

    private void OnSplashed(string kind, Vector3 at)
    {
        if (!Enum.TryParse<FishingSplashKind>(kind, out var splash) || !Enum.IsDefined(splash))
        {
            logger.LogWarning("The server sent a splash of a kind this game does not know: {Kind}.", kind);
            return;
        }

        var look = splash switch
        {
            FishingSplashKind.Cast => new SplashLook(SplashSmall, SmallSplash, SplashSound),
            FishingSplashKind.Bite => new SplashLook(SplashSmall, FullSplash, SplashSound),
            FishingSplashKind.Thrash => new SplashLook(SplashSmall, FullSplash, ThrashSound),
            FishingSplashKind.Catch => new SplashLook(SplashBig, FullSplash, SplashSound),
        };
        effects.Play(look.System, at, Vector3.Zero, look.Scale, SplashSeconds);
        sounds.PlayAt(look.Sound, at);
    }

    private void Update()
    {
        foreach (var (angler, scene) in _anglers.ToList())
        {
            var next = KeepRod(angler, scene);
            next = Glide(next);
            if (next != scene)
                _anglers[angler] = next;
        }
    }

    // The rod goes on the hand once the character is drawn here, and the hold is played again before it runs out.
    private AnglerScene KeepRod(int angler, AnglerScene scene)
    {
        if (Time.Elapsed < scene.HoldAgainAt || FindCharacter(angler) is not { } character)
            return scene;

        var rod = scene.Rod;
        if (rod is null)
        {
            var placed = props.Place(new Prop(RodMesh, character.Position, Vector3.Zero, 1f), collides: false);
            if (props.AttachTo(placed, character, CharacterMesh.ThirdPerson, HandSocket, Vector3.Zero, Vector3.Zero))
                rod = placed;
            else
                props.Remove(placed);
        }

        var isLocal = players.LocalMainCharacter is { } me && me.PlayerId == character.PlayerId;
        var seconds = isLocal ? animations.Play(Hold, null, 1f) : animations.PlayOn(character, Hold, 1f);
        var again = seconds > HoldOverlap ? seconds - HoldOverlap : HoldRetrySeconds;
        return scene with { Rod = rod, HoldAgainAt = Time.Elapsed + again };
    }

    private AnglerScene Glide(AnglerScene scene)
    {
        if (scene.Float is not { } handle || scene.FloatDrawn is not { } drawn || scene.FloatTarget is not { } target || drawn == target)
            return scene;

        var step = Vector3.Lerp(drawn, target, Math.Min(1f, FloatGlide * Time.DeltaTime));
        props.Move(handle, step, new Vector3(FloatPitch, 0f, 0f));
        return scene with { FloatDrawn = step };
    }

    private MainCharacter? FindCharacter(int angler)
    {
        foreach (var character in players.AreaPlayers)
        {
            if (character.PlayerId.RawValue == angler)
                return character;
        }

        return null;
    }

    private void Forget(int angler)
    {
        if (!_anglers.Remove(angler, out var scene))
            return;

        if (scene.Rod is { } rod)
            props.Remove(rod);
        if (scene.Float is { } handle)
            props.Remove(handle);
        if (players.LocalMainCharacter is { } me && me.PlayerId.RawValue == angler)
            animations.Stop(HoldBlendOutSeconds);
    }

    private sealed record SplashLook(string System, float Scale, string Sound);
}
