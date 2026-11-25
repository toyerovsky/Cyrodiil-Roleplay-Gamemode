using System.Numerics;
using CyrodiilRP.Common.Fishing;
using Microsoft.Extensions.Logging;
using OblivionMP.SDK;
using OblivionMP.SDK.Common.Archetypes;
using OblivionMP.SDK.Common.Props;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client.Fishing;

// Every angler this player sees, themselves included: the rod of its grade in the hand and the staff's poses, the cast's
// swing and the float's flight, the float bobbing on the water, the fish under it, the catch held up, and the splashes
// seen and heard here. The game has no fishing rod: its staves stand for one.
[Service]
internal sealed partial class AnglerScenery(
    RpcHandlers rpc,
    IPlayers players,
    ILocalProps props,
    IAnimations animations,
    IVisualEffects effects,
    ISounds sounds,
    MissingAssets missing,
    ILogger logger)
{
    // How deep a float lies under the water line with no fish at it.
    private const float Afloat = 0f;

    private readonly Dictionary<int, AnglerScene> _anglers = [];
    private bool _castSplashTold;

    private void Start()
    {
        rpc.AnglerShown += OnShown;
        rpc.AnglerCasted += OnCast;
        rpc.FishingFloatMoved += OnFloatMoved;
        rpc.FishingFloatGone += OnFloatGone;
        rpc.FishingFishMoved += OnFishMoved;
        rpc.FishingSplashed += OnSplashed;
    }

    private void Stop()
    {
        rpc.AnglerShown -= OnShown;
        rpc.AnglerCasted -= OnCast;
        rpc.FishingFloatMoved -= OnFloatMoved;
        rpc.FishingFloatGone -= OnFloatGone;
        rpc.FishingFishMoved -= OnFishMoved;
        rpc.FishingSplashed -= OnSplashed;
        foreach (var angler in _anglers.Keys.ToList())
            Forget(angler);
    }

    // Where an angler's float is drawn now, in the air too, for the angler's camera and page.
    public Vector3? FindFloat(int angler) => _anglers.TryGetValue(angler, out var scene) ? scene.Float?.Drawn : null;

    public bool IsShowingCatch(int angler) => _anglers.TryGetValue(angler, out var scene) && scene.Show is not null;

    private void OnShown(int angler, bool fishing, string rod)
    {
        if (!fishing)
        {
            Forget(angler);
            return;
        }

        if (!Enum.TryParse<RodTier>(rod, out var tier) || !Enum.IsDefined(tier))
        {
            logger.LogWarning("The server showed an angler with a rod this game does not know: {Rod}.", rod);
            return;
        }

        if (!_anglers.TryGetValue(angler, out var scene))
        {
            _anglers[angler] = new AnglerScene(tier, Rod: null, RodRetryAt: 0f, AnglerPose.Holding, PoseSince: Time.Elapsed,
                PosePlayed: false, PoseNextAt: 0f, Cast: null, Float: null, Fish: null, Show: null);
            return;
        }

        if (scene.Tier != tier)
            _anglers[angler] = RemoveRod(scene) with { Tier = tier };
    }

    private void OnCast(int angler, Vector3 at)
    {
        if (!_anglers.TryGetValue(angler, out var scene))
            return;

        var now = Time.Elapsed;
        var from = FindCharacter(angler) is { } character
            ? character.Position + FishingShot.Forward(character.Rotation) * AnglerLooks.ReleaseAhead + new Vector3(0f, 0f, AnglerLooks.ReleaseUp)
            : at;
        var distance = Vector2.Distance(new Vector2(from.X, from.Y), new Vector2(at.X, at.Y));
        var flies = now + AnglerLooks.ReleaseSeconds;
        var cast = new CastScene(from, at, CastFlight.Rise(distance), flies, flies + CastFlight.Seconds(distance), Flying: false);
        _anglers[angler] = Pose(RemoveFish(RemoveFloat(scene)) with { Cast = cast }, AnglerPose.Casting, now);
    }

    // While the float flies the server's word on it waits for the landing.
    private void OnFloatMoved(int angler, Vector3 at)
    {
        if (!_anglers.TryGetValue(angler, out var scene) || scene.Cast is not null)
            return;

        if (scene.Float is { } bobber)
        {
            var drawn = Vector3.Distance(bobber.Drawn, at) < AnglerLooks.FloatJumps ? bobber.Drawn : at;
            _anglers[angler] = scene with { Float = bobber with { Drawn = drawn, Target = at } };
            return;
        }

        _anglers[angler] = scene with { Float = PlaceFloat(at, at) };
    }

    private void OnFloatGone(int angler)
    {
        if (_anglers.TryGetValue(angler, out var scene))
            _anglers[angler] = RemoveFloat(scene);
    }

    private void OnFishMoved(int angler, string move, Vector3 at, float yaw, int lengthCm)
    {
        if (!Enum.TryParse<FishMove>(move, out var fishMove) || !Enum.IsDefined(fishMove))
        {
            logger.LogWarning("The server moved a fish in a way this game does not know: {Move}.", move);
            return;
        }

        if (!_anglers.TryGetValue(angler, out var scene))
            return;

        var now = Time.Elapsed;
        _anglers[angler] = fishMove switch
        {
            FishMove.Approach => Approach(scene, at, yaw, lengthCm, now),
            FishMove.Nibble => Nibble(Swim(scene, FishSwim.Darting, at, yaw, lengthCm, now, Under(at)), now),
            FishMove.Bite => Bite(Swim(scene, FishSwim.Diving, at, yaw, lengthCm, now, Under(at)), at),
            FishMove.Hooked => Pose(Swim(scene, FishSwim.Following, at, yaw, lengthCm, now, Under(at)), AnglerPose.Striking, now),
            FishMove.Jump => Swim(scene, FishSwim.Jumping, at, yaw, lengthCm, now, Under(at)),
            FishMove.Landed => Land(RemoveFish(scene), angler, lengthCm, now),
            FishMove.Escaped => Unbrace(Swim(scene, FishSwim.Leaving, at, yaw, lengthCm, now, Under(at)), now),
            FishMove.Gone => Unbrace(RemoveFish(scene), now),
        };
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
            FishingSplashKind.Cast => null,
            FishingSplashKind.Nibble => new SplashLook(AnglerLooks.Plop, AnglerLooks.NibbleScale, AnglerLooks.SplashSound, Ripples: true),
            FishingSplashKind.Bite => new SplashLook(AnglerLooks.Impact, AnglerLooks.BiteScale, AnglerLooks.SplashSound, Ripples: true),
            FishingSplashKind.Thrash => new SplashLook(AnglerLooks.SplashMedium, AnglerLooks.ThrashScale, AnglerLooks.ThrashSound, Ripples: false),
            FishingSplashKind.Jump => new SplashLook(AnglerLooks.SplashLarge, AnglerLooks.JumpScale, AnglerLooks.SplashSound, Ripples: false),
            FishingSplashKind.Catch => new SplashLook(AnglerLooks.SplashHuge, AnglerLooks.CatchScale, AnglerLooks.SplashSound, Ripples: true),
            FishingSplashKind.Snap => new SplashLook(AnglerLooks.SplashSmall, AnglerLooks.SnapScale, AnglerLooks.SplashSound, Ripples: false),
        };
        if (look is null)
        {
            if (!_castSplashTold)
                logger.LogWarning("The server sent a cast's splash, which every game plays itself as the float lands: it is left out.");
            _castSplashTold = true;
            return;
        }

        effects.Play(look.System, at, Vector3.Zero, look.Scale, AnglerLooks.SplashSeconds);
        if (look.Ripples)
            effects.Play(AnglerLooks.Ripples, at, Vector3.Zero, AnglerLooks.RipplesScale, AnglerLooks.RipplesSeconds);
        sounds.PlayAt(look.Sound, at);
    }

    private void Update()
    {
        var now = Time.Elapsed;
        var seconds = Time.DeltaTime;
        foreach (var (angler, scene) in _anglers.ToList())
        {
            var character = FindCharacter(angler);
            var next = KeepRod(scene, character, now);
            next = KeepPose(next, character, now);
            next = Fly(next, now);
            next = Bob(next, angler, now, seconds);
            next = SwimOn(next, now, seconds);
            next = EndShow(next, now);
            if (next != scene)
                _anglers[angler] = next;
        }
    }

    // The rod goes on the hand once the character is drawn here, and not while a catch is held up.
    private AnglerScene KeepRod(AnglerScene scene, MainCharacter? character, float now)
    {
        if (scene.Rod is not null || scene.Show is not null || character is not { } holder || now < scene.RodRetryAt)
            return scene;

        var mesh = FishingGear.MeshOf(scene.Tier);
        var placed = props.Place(new Prop(mesh, holder.Position, Vector3.Zero, AnglerLooks.RodScale), collides: false);
        if (props.AttachTo(placed, holder, CharacterMesh.ThirdPerson, AnglerLooks.HandSocket, AnglerLooks.RodOffset, AnglerLooks.RodRotation))
            return scene with { Rod = placed };

        props.Remove(placed);
        if (IsLocal(holder))
            missing.Report(mesh, "rod on " + AnglerLooks.HandSocket);
        return scene with { RodRetryAt = now + AnglerLooks.RodRetrySeconds };
    }

    // A pose that loops is played again before it ends. A move played once gives way to the next pose when it ends, or
    // when it could not be played in time.
    private AnglerScene KeepPose(AnglerScene scene, MainCharacter? character, float now)
    {
        if (character is not { } holder || now < scene.PoseNextAt)
            return scene;

        if (IsOnce(scene.Pose) && (scene.PosePlayed || now - scene.PoseSince > AnglerLooks.StalePoseSeconds))
            scene = scene with { Pose = After(scene.Pose), PoseSince = now, PosePlayed = false };

        var animation = AnimationOf(scene.Pose);
        var isLocal = IsLocal(holder);
        var played = isLocal ? animations.Play(animation, null, 1f) : animations.PlayOn(holder, animation, 1f);
        if (played <= 0f && isLocal)
            missing.Report(animation, "animation");

        var wait = played > 0f ? LastsFor(scene.Pose, played) : WaitAfterFailing(scene.Pose);
        return scene with { PosePlayed = true, PoseNextAt = now + wait };
    }

    private AnglerScene Fly(AnglerScene scene, float now)
    {
        if (scene.Cast is not { } cast || now < cast.FliesAt)
            return scene;

        var share = (now - cast.FliesAt) / (cast.LandsAt - cast.FliesAt);
        var at = CastFlight.PointAt(cast.From, cast.To, cast.Rise, share);
        var bobber = scene.Float ?? PlaceFloat(at, cast.To);
        if (!cast.Flying)
            sounds.PlayAt(AnglerLooks.WhooshSound, cast.From);

        if (share < 1f)
        {
            props.Move(bobber.Prop, at + new Vector3(0f, 0f, AnglerLooks.FloatRaise), AnglerLooks.FloatRotation);
            return scene with { Cast = cast with { Flying = true }, Float = bobber with { Drawn = at, Target = cast.To } };
        }

        effects.Play(AnglerLooks.Impact, cast.To, Vector3.Zero, AnglerLooks.LandingScale, AnglerLooks.SplashSeconds);
        sounds.PlayAt(AnglerLooks.SplashSound, cast.To);
        effects.Stop(bobber.Ripples);
        var ripples = effects.Play(AnglerLooks.Ripples, cast.To, Vector3.Zero, AnglerLooks.RipplesScale, AnglerLooks.RipplesSeconds);
        var landed = bobber with { Drawn = cast.To, Target = cast.To, Sunk = Afloat, NibbledAt = float.NegativeInfinity, Ripples = ripples };
        return scene with { Cast = null, Float = landed };
    }

    // On the water the float glides to where the server put it, bobs and dips at a nibble, and sinks when a fish takes it.
    private AnglerScene Bob(AnglerScene scene, int angler, float now, float seconds)
    {
        if (scene.Cast is not null || scene.Float is not { } bobber)
            return scene;

        var drawn = FishingGlide.Toward(bobber.Drawn, bobber.Target, AnglerLooks.FloatGlide, seconds);
        var sinking = scene.Fish is { } fish ? SinkOf(fish.Swim) : Afloat;
        var sunk = FishingGlide.Toward(bobber.Sunk, sinking, AnglerLooks.SinkGlide, seconds);
        var bob = FloatMotion.Bob(now + angler * AnglerLooks.BobPhaseSeconds) + FloatMotion.Dip(now - bobber.NibbledAt);
        props.Move(bobber.Prop, drawn + new Vector3(0f, 0f, AnglerLooks.FloatRaise - sunk + bob), AnglerLooks.FloatRotation);
        return scene with { Float = bobber with { Drawn = drawn, Sunk = sunk } };
    }

    private AnglerScene SwimOn(AnglerScene scene, float now, float seconds)
    {
        if (scene.Fish is not { } fish)
            return scene;

        var since = now - fish.Since;
        if (LastsOf(fish.Swim) is { } lasts && since >= lasts)
            return Pass(scene, fish, now);

        var center = scene.Float?.Drawn ?? fish.Point;
        var jumping = fish.Swim == FishSwim.Jumping;
        var drawn = jumping
            ? FishMotion.JumpAt(fish.Point, fish.Yaw, since / FishMotion.JumpSeconds)
            : FishingGlide.Toward(fish.Drawn, WantedOf(fish, center, now), GlideOf(fish.Swim), seconds);
        var turned = Vector3.Distance(fish.Drawn, drawn) > AnglerLooks.FishTurnsOver
            ? FishingGlide.TurnToward(fish.DrawnYaw, FishMotion.Heading(fish.Drawn, drawn, fish.DrawnYaw), AnglerLooks.FishTurn, seconds)
            : fish.DrawnYaw;
        var yaw = jumping ? fish.Yaw : turned;
        var tilt = jumping ? FishMotion.JumpTiltAt(since / FishMotion.JumpSeconds) : 0f;
        props.Move(fish.Prop, drawn, FishRotation(yaw, tilt));
        return scene with { Fish = fish with { Drawn = drawn, DrawnYaw = yaw } };
    }

    // The catch goes back to where it came from and the rod comes back to the hand.
    private AnglerScene EndShow(AnglerScene scene, float now)
    {
        if (scene.Show is not { } show || now < show.EndsAt)
            return scene;

        if (show.Fish is { } held)
            props.Remove(held);
        return scene with { Show = null };
    }

    // A new fish swims in along the yaw to circle the float.
    private AnglerScene Approach(AnglerScene scene, Vector3 at, float yaw, int lengthCm, float now)
    {
        var next = Swim(scene, FishSwim.Circling, at, yaw, lengthCm, now, Under(at - FishingShot.Forward(yaw) * AnglerLooks.ApproachFrom));
        if (next.Fish is not { } fish)
            return next;

        effects.Stop(fish.School);
        var school = effects.Play(AnglerLooks.School, at - new Vector3(0f, 0f, AnglerLooks.SchoolDepth), Vector3.Zero,
            AnglerLooks.SchoolScale, AnglerLooks.SchoolSeconds);
        return next with { Fish = fish with { School = school } };
    }

    private static AnglerScene Nibble(AnglerScene scene, float now) =>
        scene.Float is { } bobber ? scene with { Float = bobber with { NibbledAt = now } } : scene;

    private AnglerScene Bite(AnglerScene scene, Vector3 at)
    {
        effects.Play(AnglerLooks.Bubbles, at - new Vector3(0f, 0f, AnglerLooks.BubblesDepth), Vector3.Zero, AnglerLooks.BubblesScale,
            AnglerLooks.BubblesSeconds);
        return scene;
    }

    // The fish the move is about: the one drawn, or a new one placed where it comes from, again for another length.
    private AnglerScene Swim(AnglerScene scene, FishSwim swim, Vector3 at, float yaw, int lengthCm, float now, Vector3 placeAt)
    {
        var scale = FishMotion.ScaleFor(lengthCm > 0 ? lengthCm : AnglerLooks.FishShownCm);
        var fish = scene.Fish is { } known && (lengthCm <= 0 || Math.Abs(known.Scale - scale) <= known.Scale * AnglerLooks.FishRescales)
            ? known
            : PlaceFish(scene.Fish, placeAt, yaw, scale);
        return scene with { Fish = fish with { Swim = swim, Point = at, Yaw = yaw, Since = now } };
    }

    private FishScene PlaceFish(FishScene? before, Vector3 at, float yaw, float scale)
    {
        var drawn = before?.Drawn ?? at;
        var facing = before?.DrawnYaw ?? yaw;
        if (before is { } old)
            props.Remove(old.Prop);
        var prop = props.Place(new Prop(AnglerLooks.FishMesh, drawn, FishRotation(facing, 0f), scale), collides: false);
        return new FishScene(prop, scale, FishSwim.Circling, at, yaw, Since: 0f, drawn, facing, before?.School ?? VisualEffectHandle.None);
    }

    // The catch comes out of the water into the angler's hand, the rod set aside, while the angler looks at it.
    private AnglerScene Land(AnglerScene scene, int angler, int lengthCm, float now)
    {
        if (FindCharacter(angler) is not { } holder)
            return Unbrace(scene, now);

        var placed = props.Place(new Prop(AnglerLooks.FishMesh, holder.Position, Vector3.Zero, FishMotion.ScaleFor(lengthCm)), collides: false);
        LocalPropHandle? held = placed;
        if (!props.AttachTo(placed, holder, CharacterMesh.ThirdPerson, AnglerLooks.HandSocket, AnglerLooks.ShowOffset, AnglerLooks.ShowRotation))
        {
            props.Remove(placed);
            held = null;
            if (IsLocal(holder))
                missing.Report(AnglerLooks.FishMesh, "catch on " + AnglerLooks.HandSocket);
        }

        var shown = RemoveRod(scene) with { Show = new CatchShow(held, now + AnglerLooks.ShowSeconds) };
        return Pose(shown, AnglerPose.Showing, now);
    }

    // The fight is over: the rod is held out again.
    private static AnglerScene Unbrace(AnglerScene scene, float now) => scene.Pose switch
    {
        AnglerPose.Striking or AnglerPose.Fighting => Pose(scene, AnglerPose.Holding, now),
        AnglerPose.Holding or AnglerPose.Casting or AnglerPose.Showing => scene,
    };

    private AnglerScene Pass(AnglerScene scene, FishScene fish, float now) => fish.Swim switch
    {
        FishSwim.Darting => scene with { Fish = fish with { Swim = FishSwim.Circling, Since = now } },
        FishSwim.Jumping => Reenter(scene, fish, now),
        FishSwim.Leaving => RemoveFish(scene),
        FishSwim.Circling or FishSwim.Diving or FishSwim.Following => scene,
    };

    private AnglerScene Reenter(AnglerScene scene, FishScene fish, float now)
    {
        var down = FishMotion.JumpAt(fish.Point, fish.Yaw, 1f);
        effects.Play(AnglerLooks.SplashSmall, down, Vector3.Zero, AnglerLooks.ReentryScale, AnglerLooks.SplashSeconds);
        sounds.PlayAt(AnglerLooks.SplashSound, down);
        return scene with { Fish = fish with { Swim = FishSwim.Following, Since = now, Drawn = down } };
    }

    private FloatScene PlaceFloat(Vector3 at, Vector3 target)
    {
        var prop = props.Place(new Prop(AnglerLooks.FloatMesh, at, AnglerLooks.FloatRotation, AnglerLooks.FloatScale), collides: false);
        return new FloatScene(prop, at, target, Sunk: Afloat, NibbledAt: float.NegativeInfinity, VisualEffectHandle.None);
    }

    private AnglerScene RemoveRod(AnglerScene scene)
    {
        if (scene.Rod is { } rod)
            props.Remove(rod);
        return scene with { Rod = null };
    }

    private AnglerScene RemoveFloat(AnglerScene scene)
    {
        if (scene.Float is { } bobber)
        {
            props.Remove(bobber.Prop);
            effects.Stop(bobber.Ripples);
        }

        return scene with { Float = null, Cast = null };
    }

    private AnglerScene RemoveFish(AnglerScene scene)
    {
        if (scene.Fish is { } fish)
        {
            props.Remove(fish.Prop);
            effects.Stop(fish.School);
        }

        return scene with { Fish = null };
    }

    private void Forget(int angler)
    {
        if (!_anglers.Remove(angler, out var scene))
            return;

        scene = RemoveFish(RemoveFloat(RemoveRod(scene)));
        if (scene.Show is { Fish: { } held })
            props.Remove(held);
        if (players.LocalMainCharacter is { } me && me.PlayerId.RawValue == angler)
            animations.Stop(AnglerLooks.PoseBlendOutSeconds);
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

    private static Vector3 Under(Vector3 surface) => surface - new Vector3(0f, 0f, FishMotion.Depth);

    private bool IsLocal(MainCharacter character) => players.LocalMainCharacter is { } me && me.PlayerId == character.PlayerId;

    private static AnglerScene Pose(AnglerScene scene, AnglerPose pose, float now) =>
        scene with { Pose = pose, PoseSince = now, PosePlayed = false, PoseNextAt = 0f };

    private static Vector3 FishRotation(float yaw, float tilt) =>
        new(0f, yaw + AnglerLooks.FishMeshYaw, tilt * AnglerLooks.FishTiltSign);

    private static Vector3 WantedOf(FishScene fish, Vector3 center, float now) => fish.Swim switch
    {
        FishSwim.Circling => FishMotion.CircleAt(center, now),
        FishSwim.Darting => FishMotion.DartAt(center, fish.Drawn),
        FishSwim.Diving => center - new Vector3(0f, 0f, AnglerLooks.BiteDepth),
        FishSwim.Following or FishSwim.Jumping => center - new Vector3(0f, 0f, AnglerLooks.FightDepth),
        FishSwim.Leaving => FishMotion.EscapeAt(fish.Point, fish.Yaw, now - fish.Since),
    };

    private static float GlideOf(FishSwim swim) => swim switch
    {
        FishSwim.Circling => AnglerLooks.CircleGlide,
        FishSwim.Darting => AnglerLooks.DartGlide,
        FishSwim.Diving => AnglerLooks.DiveGlide,
        FishSwim.Following or FishSwim.Jumping => AnglerLooks.FollowGlide,
        FishSwim.Leaving => AnglerLooks.LeaveGlide,
    };

    private static float? LastsOf(FishSwim swim) => swim switch
    {
        FishSwim.Darting => AnglerLooks.DartSeconds,
        FishSwim.Jumping => FishMotion.JumpSeconds,
        FishSwim.Leaving => AnglerLooks.EscapeSeconds,
        FishSwim.Circling or FishSwim.Diving or FishSwim.Following => null,
    };

    // How deep the float is pulled under by what the fish does.
    private static float SinkOf(FishSwim swim) => swim switch
    {
        FishSwim.Circling or FishSwim.Darting or FishSwim.Leaving => Afloat,
        FishSwim.Diving => AnglerLooks.BittenDepth,
        FishSwim.Following or FishSwim.Jumping => AnglerLooks.DraggedDepth,
    };

    private static bool IsOnce(AnglerPose pose) => pose switch
    {
        AnglerPose.Casting or AnglerPose.Striking or AnglerPose.Showing => true,
        AnglerPose.Holding or AnglerPose.Fighting => false,
    };

    private static AnglerPose After(AnglerPose pose) => pose switch
    {
        AnglerPose.Casting or AnglerPose.Showing or AnglerPose.Holding => AnglerPose.Holding,
        AnglerPose.Striking or AnglerPose.Fighting => AnglerPose.Fighting,
    };

    private static string AnimationOf(AnglerPose pose) => pose switch
    {
        AnglerPose.Holding => AnglerLooks.Holding,
        AnglerPose.Casting => AnglerLooks.Casting,
        AnglerPose.Striking => AnglerLooks.Striking,
        AnglerPose.Fighting => AnglerLooks.Fighting,
        AnglerPose.Showing => AnglerLooks.Showing,
    };

    // The catch is held up for the show's time, however long its animation runs.
    private static float LastsFor(AnglerPose pose, float played) => pose switch
    {
        AnglerPose.Showing => AnglerLooks.ShowSeconds,
        AnglerPose.Holding or AnglerPose.Casting or AnglerPose.Striking or AnglerPose.Fighting =>
            Math.Max(played - AnglerLooks.PoseOverlap, AnglerLooks.PoseShortest),
    };

    private static float WaitAfterFailing(AnglerPose pose) => pose switch
    {
        AnglerPose.Holding or AnglerPose.Fighting => AnglerLooks.PoseRetrySeconds,
        AnglerPose.Casting or AnglerPose.Striking => 0f,
        AnglerPose.Showing => AnglerLooks.ShowSeconds,
    };

    private sealed record SplashLook(string System, float Scale, string Sound, bool Ripples);
}
