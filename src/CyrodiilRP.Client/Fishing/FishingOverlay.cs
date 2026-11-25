using System.Numerics;
using CyrodiilRP.Client.Chat;
using CyrodiilRP.Client.Pages;
using CyrodiilRP.Client.Talk;
using CyrodiilRP.Common.Fishing;
using OblivionMP.SDK;
using OblivionMP.SDK.Archetypes.Mixins;
using OblivionMpCSharpMod.Input;
using ReadyM.Cef;
using ReadyM.Cef.Rpc;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client.Fishing;

// The player's own fishing. Asked by the server, it looks for water around the character. While the server holds the
// character fishing it puts the camera over the shoulder, aims the cast where the player looks, sends the cast and the
// space key's presses, and shows the fishing page over the game, which never takes the input. A move key stops it.
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
    PageInput pageInput,
    ChatOverlay chat,
    GameCursor cursor)
{
    private const string PageName = "fishing";
    // A character's position is the middle of its capsule.
    private const float FeetBelowPosition = 90f;
    // The ways round from where the player looks that the water is looked for: ahead first, behind last.
    private static readonly float[] SearchTurns = [0f, -30f, 30f, -60f, 60f, 180f];
    private const float MoveStops = 0.5f;
    private const float CameraGlide = 4f;
    private const float CameraBlendInSeconds = 0.7f;
    private const float CameraBlendOutSeconds = 0.5f;
    // An aim this many degrees from the last one is looked for again.
    private const float AimMoves = 0.5f;
    // A move of the anchor smaller than this share of the screen is not worth a message.
    private const float AnchorMoves = 0.002f;
    private const float MsPerSecond = 1000f;
    private const int NoReaction = -1;

    private OverlayPage? _page;
    private AnglerState? _fishing;
    private string? _view;
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
            if (FindCast(feet, yaw, reach) is { } cast)
                return new WaterFound(FishingVerdict.Found, yaw, cast.Z);
        }

        return WaterFound.Refused(FishingVerdict.NoWater);
    }

    // Where a cast along the yaw lands: the farthest open water deep enough the rod reaches. Water under a quay, a pier,
    // a bridge or a rock is covered, and a float does not land in it.
    private Vector3? FindCast(Vector3 feet, float yaw, float reach)
    {
        foreach (var point in FishingShot.CastPoints(feet, yaw, reach))
        {
            if (water.FindBelow(point with { Z = feet.Z + FishingShot.LookFromAbove }, FishingShot.LookDown) is { Covered: false } found
                && (found.Depth is null || found.Depth >= FishingReach.ShallowestWater))
                return point with { Z = found.SurfaceZ };
        }

        return null;
    }

    // The first aim begins the fishing; every later one is the line in again after a catch or a loss.
    private void OnAimed(float yaw, float waterZ, float reach)
    {
        if (_fishing is { } fishing)
        {
            _fishing = fishing with { Aiming = true, BiteHeardAt = null };
            return;
        }

        if (players.LocalMainCharacter is not { } me)
            return;

        var stand = me.Position - new Vector3(0f, 0f, FeetBelowPosition);
        var lookAt = stand + FishingShot.Forward(yaw) * reach;
        _fishing = new AnglerState(stand, yaw, waterZ, reach, Aiming: true, AimYaw: null, AimAt: null, LookingAt: lookAt with { Z = waterZ },
            SpaceDown: input.IsKeyDown(Key.SPACE), BiteHeardAt: null, StopAsked: false, AnchorSent: null);
        camera.Set(FishingShot.CameraAt(stand, yaw, waterZ), lookAt with { Z = waterZ }, CameraBlendInSeconds);
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
        camera.Reset(CameraBlendOutSeconds);
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
        var keysFree = !pageInput.IsHeld && !chat.IsOpen && !cursor.IsGameMenuOpen;
        if (keysFree && !fishing.StopAsked && input.MoveAxis.Length() > MoveStops)
        {
            rpc.SendFishingStop();
            fishing = fishing with { StopAsked = true };
        }

        fishing = Aim(fishing);
        var down = keysFree && input.IsKeyDown(Key.SPACE);
        if (down != fishing.SpaceDown)
            fishing = Press(fishing with { SpaceDown = down }, down);

        var floatAt = scenery.FindFloat(me.PlayerId.RawValue);
        fishing = Film(fishing, floatAt ?? fishing.AimAt);
        _fishing = Anchor(fishing, floatAt ?? fishing.AimAt);
    }

    // Looked for again only when the player turned the view far enough.
    private AnglerState Aim(AnglerState fishing)
    {
        if (!fishing.Aiming || camera.Look is not { } look)
            return fishing;

        var yaw = FishingShot.AimYaw(fishing.Yaw, look.Yaw);
        if (fishing.AimYaw is { } before && Math.Abs(FishingShot.Turn(yaw - before)) < AimMoves)
            return fishing;

        return fishing with { AimYaw = yaw, AimAt = FindCast(fishing.Stand, yaw, fishing.Reach) };
    }

    private AnglerState Press(AnglerState fishing, bool down)
    {
        if (fishing.Aiming)
        {
            if (!down || fishing.AimAt is not { } cast)
                return fishing;

            rpc.SendFishingCast(cast.X, cast.Y, cast.Z);
            return fishing with { Aiming = false };
        }

        var reaction = down && fishing.BiteHeardAt is { } heard ? (int)((Time.Elapsed - heard) * MsPerSecond) : NoReaction;
        rpc.SendFishingSpace(down, reaction);
        return down ? fishing with { BiteHeardAt = null } : fishing;
    }

    // The camera stays over the shoulder and glides to look at the float, or where the cast would land.
    private AnglerState Film(AnglerState fishing, Vector3? target)
    {
        var wanted = target ?? fishing.Stand + FishingShot.Forward(fishing.AimYaw ?? fishing.Yaw) * fishing.Reach;
        var lookingAt = Vector3.Lerp(fishing.LookingAt, wanted with { Z = target?.Z ?? fishing.WaterZ }, Math.Min(1f, CameraGlide * Time.DeltaTime));
        if (lookingAt == fishing.LookingAt)
            return fishing;

        camera.Set(FishingShot.CameraAt(fishing.Stand, fishing.Yaw, fishing.WaterZ), lookingAt);
        return fishing with { LookingAt = lookingAt };
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

    // Stand: the feet. Reach: the rod's farthest cast. LookingAt: where the camera looks now, gliding after what it should.
    private sealed record AnglerState(
        Vector3 Stand,
        float Yaw,
        float WaterZ,
        float Reach,
        bool Aiming,
        float? AimYaw,
        Vector3? AimAt,
        Vector3 LookingAt,
        bool SpaceDown,
        float? BiteHeardAt,
        bool StopAsked,
        Vector2? AnchorSent);

    private sealed record WaterFound(FishingVerdict Verdict, float Yaw, float WaterZ)
    {
        public static WaterFound Refused(FishingVerdict verdict) => new(verdict, 0f, 0f);
    }
}
