using System.Numerics;
using System.Text.Json;
using CyrodiilRP.Client.Chat;
using CyrodiilRP.Client.Pages;
using CyrodiilRP.Client.Talk;
using CyrodiilRP.Common.Fishing;
using Microsoft.Extensions.Logging;
using OblivionMP.SDK;
using OblivionMP.SDK.Archetypes.Mixins;
using OblivionMpCSharpMod.Input;
using ReadyM.Cef;
using ReadyM.Cef.Rpc;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client.Fishing;

// The player's own fishing. Asked by the server, it looks for water around the character. While the server holds the
// character fishing it aims the cast where the player looks, charges it while Space is held and throws it as far as the
// power took it, then sends Space (the strike, the reel), the rod's lean (A, D) and the stop (E). It moves the camera
// with the line, plays the angler's own sounds and shows the fishing page over the game, which never takes the input.
[Service]
internal sealed partial class FishingOverlay(
    CefHost cef,
    WebPages pages,
    PageLocale locale,
    PawnWatch pawn,
    RpcHandlers rpc,
    IPlayers players,
    ICamera camera,
    IInput input,
    IWater water,
    AnglerScenery scenery,
    AnglerCamera shots,
    AnglerCues cues,
    PageInput pageInput,
    ChatOverlay chat,
    GameCursor cursor,
    ILogger logger)
{
    private const string PageName = "fishing";
    // The game leaves X alone: E would also activate whatever stands in front.
    private const Key StopKey = Key.X;
    // A character's position is the middle of its capsule.
    private const float FeetBelowPosition = 90f;
    // The ways round from where the player looks that the water is looked for: ahead first, behind last.
    private static readonly float[] SearchTurns = [0f, -30f, 30f, -60f, 60f, 180f];
    // An aim this many degrees from the last one is looked for again.
    private const float AimMoves = 0.5f;
    // A move of the anchor smaller than this share of the screen is not worth a message.
    private const float AnchorMoves = 0.002f;
    private const float MsPerSecond = 1000f;
    private const int NoReaction = -1;
    // The server takes one change of Space or of the lean in 60 ms, so the game sends none sooner than this.
    private const float InputPaceSeconds = 0.08f;
    private const int LeanLeft = -1;
    private const int LeanNone = 0;
    private const int LeanRight = 1;
    private const int ChargeUpMs = (int)(FishingCharge.UpSeconds * MsPerSecond);

    private OverlayPage? _page;
    private AnglerState? _fishing;
    private string? _view;
    private AnglerView? _read;
    private bool _unreadTold;
    private int? _shownFor;

    private OverlayPage Page => _page ??= new OverlayPage(pages, locale, PageName, WebPages.FishingLayer, SendLocale);

    private void Start()
    {
        rpc.FishingLookAsked += OnLookAsked;
        rpc.FishingAimed += OnAimed;
        rpc.FishingPageChanged += OnPageChanged;
        rpc.FishingBitten += OnBitten;
        rpc.FishingEnded += End;
        locale.Changed += Page.ApplyLocale;
    }

    private void Stop()
    {
        rpc.FishingLookAsked -= OnLookAsked;
        rpc.FishingAimed -= OnAimed;
        rpc.FishingPageChanged -= OnPageChanged;
        rpc.FishingBitten -= OnBitten;
        rpc.FishingEnded -= End;
        locale.Changed -= Page.ApplyLocale;
        End();
    }

    private void OnLookAsked(float reach)
    {
        var found = FindWater(reach);
        rpc.SendFishingSpot(found.Verdict.ToString(), found.Yaw, found.WaterZ);
    }

    private WaterFound FindWater(float reach)
    {
        if (players.LocalMainCharacter is not { } me || me.Pawn == IntPtr.Zero)
            return WaterFound.Refused(FishingVerdict.NotInWorld);

        if (water.LocalCharacter is { Swimming: true })
            return WaterFound.Refused(FishingVerdict.Swimming);

        var feet = me.Position - new Vector3(0f, 0f, FeetBelowPosition);
        var facing = camera.Look?.Yaw ?? me.Rotation;
        foreach (var turn in SearchTurns)
        {
            var yaw = facing + turn;
            if (FindCast(feet, yaw, reach, sameWaterAs: null) is { } cast)
                return new WaterFound(FishingVerdict.Found, yaw, cast.Z);
        }

        return WaterFound.Refused(FishingVerdict.NoWater);
    }

    // Where the farthest cast along the yaw lands: the farthest open water deep enough the rod reaches. sameWaterAs: once
    // fishing, the surface the server holds the angler to (null while looking for one).
    private Vector3? FindCast(Vector3 feet, float yaw, float reach, float? sameWaterAs)
    {
        foreach (var point in FishingShot.CastPoints(feet, yaw, reach))
        {
            if (FindOpenWater(point, feet.Z, sameWaterAs) is { } found)
                return found;
        }

        return null;
    }

    // Where a throw this far lands: the nearest open water at or before it along the yaw, or past it for a short one.
    private Vector3? FindLanding(Vector3 feet, float yaw, float distance, float reach, float waterZ)
    {
        foreach (var point in CastFlight.LandingPoints(feet, yaw, distance, reach))
        {
            if (FindOpenWater(point, feet.Z, waterZ) is { } found)
                return found;
        }

        return null;
    }

    // Water under a quay, a pier, a bridge or a rock is covered, and a float does not land in it; nor in water whose
    // surface lies too far from the angler's, which the server refuses.
    private Vector3? FindOpenWater(Vector3 point, float feetZ, float? sameWaterAs) =>
        water.FindBelow(point with { Z = feetZ + FishingShot.LookFromAbove }, FishingShot.LookDown) is { Covered: false } found
        && (found.Depth is null || found.Depth >= FishingReach.ShallowestWater)
        && (sameWaterAs is not { } surface || Math.Abs(found.SurfaceZ - surface) <= FishingReach.SameWater)
            ? point with { Z = found.SurfaceZ }
            : null;

    // The first aim begins the fishing; every later one is the line in again after a catch or a loss.
    private void OnAimed(float yaw, float waterZ, float reach)
    {
        if (_fishing is { } fishing)
        {
            _fishing = fishing with
            {
                Yaw = yaw, WaterZ = waterZ, Reach = reach, Aiming = true, ChargeFrom = null, AimYaw = null, AimAt = null, SpaceSent = false,
                BiteHeardAt = null,
            };
            return;
        }

        if (players.LocalMainCharacter is not { } me)
            return;

        var stand = me.Position - new Vector3(0f, 0f, FeetBelowPosition);
        var lookAt = (stand + FishingShot.Forward(yaw) * reach) with { Z = waterZ };
        _fishing = new AnglerState(stand, yaw, waterZ, reach, Aiming: true, ChargeFrom: null, AimYaw: null, AimAt: null,
            SpaceDown: input.IsKeyDown(Key.SPACE), SpaceDownAt: 0f, SpaceSent: false, LeanSent: null, InputSentAt: float.NegativeInfinity,
            StopDown: input.IsKeyDown(StopKey), StopAsked: false, BiteHeardAt: null, AnchorSent: null);
        shots.Begin(AnglerFraming.Shoulder(stand, yaw, waterZ, lookAt), Time.Elapsed);
        cues.Begin(_read);
        _shownFor = null;
        var page = Page.Open();
        if (Page.IsReady && _view is { } view)
            SendState(page.Browser, view);
    }

    private void OnPageChanged(string json)
    {
        _view = json;
        if (Page.Opened is { } page && Page.IsReady)
            SendState(page.Browser, json);

        if (Read(json) is not { } read)
            return;

        _read = read;
        if (_fishing is { } fishing && players.LocalMainCharacter is { } me)
            cues.Hear(read, me.Position, AnglerFraming.HandAt(fishing.Stand, fishing.Yaw));
    }

    // A view this game cannot read is told once a fishing: the views come many a second in a fight.
    private AnglerView? Read(string json)
    {
        try
        {
            var read = JsonSerializer.Deserialize(json, AnglerViewJson.Default.AnglerView);
            if (read is null && !_unreadTold)
            {
                logger.LogWarning("The server sent an empty fishing view.");
                _unreadTold = true;
            }

            return read;
        }
        catch (JsonException ex)
        {
            if (!_unreadTold)
                logger.LogWarning(ex, "The server sent a fishing view this game cannot read: {Json}.", json);
            _unreadTold = true;
            return null;
        }
    }

    private void OnBitten()
    {
        if (_fishing is { } fishing)
            _fishing = fishing with { BiteHeardAt = Time.Elapsed };
    }

    private void End()
    {
        if (_fishing is null)
            return;

        _fishing = null;
        _view = null;
        _read = null;
        _unreadTold = false;
        shots.End();
        cues.End();
        if (Page.Opened is not { } page)
            return;

        if (Page.IsReady)
            SendEnded(page.Browser);
        cef.SetVisible(page.Viewport, false);
        _shownFor = null;
    }

    private void Update()
    {
        if (_fishing is not { } fishing || players.LocalMainCharacter is not { } me)
            return;

        ShowPage();
        var now = Time.Elapsed;
        var keysFree = !pageInput.IsHeld && !chat.IsOpen && !cursor.IsGameMenuOpen;
        fishing = AskStop(fishing, keysFree && input.IsKeyDown(StopKey));
        fishing = Aim(fishing);
        var spaceDown = keysFree && input.IsKeyDown(Key.SPACE);
        fishing = fishing.Aiming ? Charge(fishing, spaceDown, keysFree, now) : Reel(fishing, spaceDown, now);
        fishing = Lean(fishing, keysFree, now);

        var self = me.PlayerId.RawValue;
        var floatAt = scenery.FindFloat(self);
        Film(fishing, scenery.FindFloatGoal(self), scenery.IsShowingCatch(self), now);
        cues.Keep(_read, me.Position, now);
        _fishing = Anchor(fishing, Marked(fishing, floatAt, now));
    }

    private AnglerState AskStop(AnglerState fishing, bool down)
    {
        if (down == fishing.StopDown)
            return fishing;

        if (down && !fishing.StopAsked)
        {
            rpc.SendFishingStop();
            return fishing with { StopDown = true, StopAsked = true };
        }

        return fishing with { StopDown = down };
    }

    // Looked for again only when the player turned the view far enough.
    private AnglerState Aim(AnglerState fishing)
    {
        if (!fishing.Aiming || camera.Look is not { } look)
            return fishing;

        var yaw = FishingShot.AimYaw(fishing.Yaw, look.Yaw);
        if (fishing.AimYaw is { } before && Math.Abs(FishingShot.Turn(yaw - before)) < AimMoves)
            return fishing;

        return fishing with { AimYaw = yaw, AimAt = FindCast(fishing.Stand, yaw, fishing.Reach, fishing.WaterZ) };
    }

    // Space pressed with water along the aim charges the cast, let go throws it. A page or the chat taking the keys
    // meanwhile puts the rod down without a throw.
    private AnglerState Charge(AnglerState fishing, bool down, bool keysFree, float now)
    {
        var pressed = down && !fishing.SpaceDown;
        fishing = fishing with { SpaceDown = down };
        if (fishing.ChargeFrom is { } from)
        {
            if (!keysFree)
            {
                TellCharge(charging: false);
                return fishing with { ChargeFrom = null };
            }

            return down ? fishing : Throw(fishing, now - from);
        }

        if (!pressed || fishing.AimAt is null)
            return fishing;

        TellCharge(charging: true);
        return fishing with { ChargeFrom = now };
    }

    private AnglerState Throw(AnglerState fishing, float heldSeconds)
    {
        TellCharge(charging: false);
        var thrown = fishing with { ChargeFrom = null };
        if (fishing.AimYaw is not { } yaw)
            return thrown;

        var distance = CastFlight.Distance(FishingCharge.PowerAt(Math.Max(0f, heldSeconds)), fishing.Reach);
        if ((FindLanding(fishing.Stand, yaw, distance, fishing.Reach, fishing.WaterZ) ?? fishing.AimAt) is not { } landing)
            return thrown;

        rpc.SendFishingCast(landing.X, landing.Y, landing.Z);
        return thrown with { Aiming = false, SpaceSent = false };
    }

    // Space after the cast goes to the server: a press soon after the bite strikes, held it reels. A change waits for the
    // server's pace, and the reaction is counted from when the key went down.
    private AnglerState Reel(AnglerState fishing, bool down, float now)
    {
        if (down != fishing.SpaceDown)
            fishing = down ? fishing with { SpaceDown = true, SpaceDownAt = now } : fishing with { SpaceDown = false };

        if (fishing.SpaceDown == fishing.SpaceSent || now - fishing.InputSentAt < InputPaceSeconds)
            return fishing;

        var reaction = fishing.SpaceDown && fishing.BiteHeardAt is { } heard && fishing.SpaceDownAt >= heard
            ? (int)((fishing.SpaceDownAt - heard) * MsPerSecond)
            : NoReaction;
        rpc.SendFishingSpace(fishing.SpaceDown, reaction);
        return fishing with
        {
            SpaceSent = fishing.SpaceDown,
            InputSentAt = now,
            BiteHeardAt = fishing.SpaceDown ? null : fishing.BiteHeardAt,
        };
    }

    // A and D lean the rod while a fish is on the line. The lean the server took starts each fight.
    private AnglerState Lean(AnglerState fishing, bool keysFree, float now)
    {
        if (_read is not { Stage: LineStage.Fight } read)
            return fishing.LeanSent is null ? fishing : fishing with { LeanSent = null };

        var sent = fishing.LeanSent ?? read.Fight?.Lean ?? LeanNone;
        var left = keysFree && input.IsKeyDown(Key.A) ? LeanLeft : LeanNone;
        var right = keysFree && input.IsKeyDown(Key.D) ? LeanRight : LeanNone;
        var wanted = left + right;
        if (wanted == sent || now - fishing.InputSentAt < InputPaceSeconds)
            return fishing.LeanSent == sent ? fishing : fishing with { LeanSent = sent };

        rpc.SendFishingLean(wanted);
        return fishing with { LeanSent = wanted, InputSentAt = now };
    }

    // Over the shoulder at the aim or the float, closer at the bite, after the float in the fight, round to the front for
    // the catch held up.
    private void Film(AnglerState fishing, Vector3? floatAt, bool showingCatch, float now)
    {
        var target = floatAt ?? fishing.AimAt;
        var lookAt = target ?? (fishing.Stand + FishingShot.Forward(fishing.AimYaw ?? fishing.Yaw) * fishing.Reach) with { Z = fishing.WaterZ };
        var shoulder = AnglerFraming.Shoulder(fishing.Stand, fishing.Yaw, fishing.WaterZ, lookAt);
        var stage = _read?.Stage ?? LineStage.Aim;
        var shot = showingCatch ? AnglerShot.Catch
            : floatAt is null ? AnglerShot.Shoulder
            : stage switch
            {
                LineStage.Bite => AnglerShot.Bite,
                LineStage.Fight => AnglerShot.Fight,
                LineStage.Aim or LineStage.Waiting or LineStage.Rest => AnglerShot.Shoulder,
            };
        var frame = shot switch
        {
            AnglerShot.Shoulder => shoulder,
            AnglerShot.Bite => AnglerFraming.Bite(shoulder, lookAt),
            AnglerShot.Fight => AnglerFraming.Fight(fishing.Stand, fishing.Yaw, fishing.WaterZ, lookAt),
            AnglerShot.Catch => AnglerFraming.Catch(fishing.Stand, fishing.Yaw),
        };
        shots.Film(shot, frame, now, Time.DeltaTime);
    }

    // What the page marks: while charging, where the power would throw the float, else the float or the aim.
    private static Vector3? Marked(AnglerState fishing, Vector3? floatAt, float now)
    {
        if (fishing.ChargeFrom is not { } from || fishing.AimYaw is not { } yaw)
            return floatAt ?? fishing.AimAt;

        var distance = CastFlight.Distance(FishingCharge.PowerAt(Math.Max(0f, now - from)), fishing.Reach);
        return (fishing.Stand + FishingShot.Forward(yaw) * distance) with { Z = fishing.WaterZ };
    }

    // Where on the screen the page draws the float's marks: told only when it moved.
    private AnglerState Anchor(AnglerState fishing, Vector3? point)
    {
        Vector2? shown = point is { } at && camera.TryProject(at, out var screen) ? screen : null;
        var moved = shown is { } now && fishing.AnchorSent is { } before
            ? Vector2.Distance(now, before) >= AnchorMoves
            : shown != fishing.AnchorSent;
        if (!moved || Page.Opened is not { } page || !Page.IsReady)
            return fishing;

        SendAnchor(page.Browser, shown?.X ?? -1f, shown?.Y ?? -1f, shown is not null);
        return fishing with { AnchorSent = shown };
    }

    private void TellCharge(bool charging)
    {
        if (Page.Opened is { } page && Page.IsReady)
            SendCharge(page.Browser, charging, ChargeUpMs);
    }

    // The game builds its overlay anew after every load: the page is shown again once the pawn has settled.
    private void ShowPage()
    {
        if (Page.Opened is not { } page || !Page.IsReady || _shownFor == pawn.Number || !pawn.HasSettled(WebPages.OverlaySettleSeconds))
            return;

        cef.SetVisible(page.Viewport, true);
        _shownFor = pawn.Number;
    }

    [ReceiveFromCef]
    private void OnFishingReady(BrowserHandle browser)
    {
        Page.MarkReady(browser);
        if (_fishing is not null && _view is { } view)
            SendState(browser, view);
    }

    [SendToCef]
    private partial void SendLocale(BrowserHandle browser, string locale);

    [SendToCef]
    private partial void SendState(BrowserHandle browser, string json);

    // x, y: shares of the screen from its top left, for a point in front of the view.
    [SendToCef]
    private partial void SendAnchor(BrowserHandle browser, float x, float y, bool shown);

    [SendToCef]
    private partial void SendEnded(BrowserHandle browser);

    // Space went down with the cast to charge (the page runs the power from now, up in so many ms), or up, or was let go.
    [SendToCef]
    private partial void SendCharge(BrowserHandle browser, bool charging, int upMs);

    // Stand: the feet. Reach: the rod's farthest cast. ChargeFrom: when Space went down for the cast. SpaceSent, LeanSent
    // and InputSentAt: what the server was told last, and when.
    private sealed record AnglerState(
        Vector3 Stand,
        float Yaw,
        float WaterZ,
        float Reach,
        bool Aiming,
        float? ChargeFrom,
        float? AimYaw,
        Vector3? AimAt,
        bool SpaceDown,
        float SpaceDownAt,
        bool SpaceSent,
        int? LeanSent,
        float InputSentAt,
        bool StopDown,
        bool StopAsked,
        float? BiteHeardAt,
        Vector2? AnchorSent);

    private sealed record WaterFound(FishingVerdict Verdict, float Yaw, float WaterZ)
    {
        public static WaterFound Refused(FishingVerdict verdict) => new(verdict, 0f, 0f);
    }
}
