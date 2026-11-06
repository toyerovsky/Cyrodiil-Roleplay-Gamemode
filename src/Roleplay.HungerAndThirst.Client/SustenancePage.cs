using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;
using OblivionMP.SDK;
using ReadyM.Cef;
using ReadyM.Cef.Rpc;
using ReadyM.SDK.Attributes;
using OblivionMpCSharpMod.Input;
using Roleplay.HungerAndThirst.Common;

namespace Roleplay.HungerAndThirst.Client;

/// <summary>
/// The two web pages (web\, plain HTML) that draw the bars and the satchel, each in a viewport of its own.
/// <para>Two viewports because of the mouse. CEF's input mode is Exclusive unless some mod changes it, and that is
/// global: while the game shows a cursor (any vanilla menu), a visible overlay takes every click on its whole rectangle.
/// One full-screen HUD overlay therefore made the game's own menus deaf to the mouse. So the bars live in a viewport
/// only as big as the bars, collapsed while a menu is up, and the satchel in a full-screen one that is collapsed unless
/// it is open.</para>
/// <para>A vanilla menu is open while the game shows its mouse cursor: the inventory, containers, barter, the map,
/// dialogue, pause and wait all do. The SDK tells mods nothing about menus, but it hands every page the cursor through
/// its hit-test hook, so the pages report when it comes and goes (hud.js, HatCursor). Guessing from the menu keys,
/// as the first version did, missed menus opened with E and drifted out of step when Esc was pressed quickly.
/// PlayerActivity.InDialogue is not used: it reads the player's conversation pointer, which is also set while NPCs
/// nearby talk to each other (two of them gossiping in an inn hid the bars). Talking to someone opens the dialogue
/// menu, and its cursor already hides the bars.</para>
/// <para>A load forgets the cursor. The loading screen shows it, so the pages report it just before their overlay
/// widgets are destroyed, and with no widgets during the load nobody is told when it goes. So on leaving the world and
/// on every re-arm the flag is cleared and the pages are told to forget what they reported (cursorEpoch): a page
/// whose cursor really is up reports it again on the next tick, since the hook is called every tick while it is.
/// Reports carry the epoch, so one still on its way from before the load is ignored.</para>
/// <para>While the satchel is closed nothing takes focus, so the game keeps every key and click. Alt+H (Polled, so it
/// also works while the page has the keyboard) opens the satchel: its page takes the keyboard focus, and Up, Down,
/// Enter and Esc reach it. 1.0 pages get no mouse.</para>
/// <para>The game's input mode is never touched, the same as the chat mod. SetUiInputMode(false) is Unreal's
/// SetInputModeGameOnly, not "back to how it was": after it the game's own menus stop taking the mouse and only
/// keyboard navigation works, and the SDK has no way to restore the mode the game had.</para>
/// <para>Shown only once the character is in the world, and again after every load. Showing a viewport is what
/// binds it to the overlay widget's texture (CEF's Arm), and that widget only exists in gameplay: shown at Start, the
/// arm finds nothing, fails without a word and is never tried again.</para>
/// <para>Game to pages: the whole state as one JSON message, the same to both, whenever it changes and once a second
/// regardless, so a page that loaded late or missed a message catches up. Pages to game: ready, use, close and lost
/// focus.</para>
/// </summary>
[Service]
public sealed partial class SustenancePage(CefHost cef, IInput input, IPlayers players, SustenanceState state, ILogger logger)
{
    private const string Host = "hungerandthirst";

    // Above chat's bar (0), below MESOL's card (6) and RpRoll's board (7). The satchel sits over the bars.
    private const int HudZOrder = 2;
    private const int SatchelZOrder = 3;

    // Where the bars go, as a share of the screen: left 2% to 17% across, 79.5% to 84.5% down. The bars fill this
    // rectangle (hud.css lays them out in container units), so it sets their size at every resolution.
    private static readonly ViewportPlacement HudPlacement = new()
    {
        AnchorMinX = 0.02f,
        AnchorMaxX = 0.17f,
        AnchorMinY = 0.795f,
        AnchorMaxY = 0.845f,
    };

    private const double KeepAliveSeconds = 1.0;

    // After the character appears, wait this long before showing: the overlay widget is spawned around the same
    // moment, and arming before it exists does nothing.
    private const double ShowDelaySeconds = 1.0;
    private const double MessageSeconds = 3.0;

    /// <summary>AppDomain slot another mod's page sets while the player types in it (shared with RpRoll and MESOL).</summary>
    public const string TextInputKey = "OBMP.TextInputActive";

    /// <summary>AppDomain slot that is true while the satchel is open, for other mods' keys.</summary>
    public const string SatchelOpenKey = "HungerAndThirst.SatchelOpen";

    // Named explicitly in case the host ever switches reflection-based JSON off, as the AOT relay server does.
    private static readonly JsonSerializerOptions Json = new() { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private string? _folder;
    private IDisposable? _key;
    private ViewportHandle _hudViewport;
    private ViewportHandle _satchelViewport;
    private BrowserHandle? _hudPage;
    private BrowserHandle? _satchelPage;
    private bool _open;
    private bool _hudEnabled = true;    // false: another mod draws hunger and thirst itself (ILocalSustenance)
    private bool _hudShown;     // the HUD viewport is on screen (in the world, no menu, no dialogue)
    private bool _hudArmed;     // shown at least once since the character last appeared
    private string _message = string.Empty;
    private double _messageUntil;
    private string _lastJson = string.Empty;
    private double _lastPush = double.NegativeInfinity;
    private long _seq;
    private bool _inWorld;
    private double _showAt = double.PositiveInfinity;
    private bool _gameCursor;   // the game shows its mouse cursor, so one of its menus is open
    private long _cursorEpoch;  // bumped when the cursor state is forgotten; reports from older epochs are ignored
    private string _hiddenFor = string.Empty;   // why the bars are hidden, for the log

    private double Now => _clock.Elapsed.TotalSeconds;

    /// <summary>The satchel is open. Set it to open or close it, as Alt+H does.</summary>
    public bool SatchelOpen
    {
        get => _open;
        set => Guard("opening or closing the satchel", () => SetOpen(value));
    }

    /// <summary>False keeps the bars off screen for good, for a mod that draws its own.</summary>
    public bool HudEnabled
    {
        get => _hudEnabled;
        set => _hudEnabled = value;
    }

    /// <summary>The client mod folder's web\. Set by the entry point before any service starts.</summary>
    public void UseFolder(string folder)
    {
        _folder = folder;
    }

    private void Start()
    {
        if (_folder is null)
        {
            logger.LogError("[HungerAndThirst] No web folder, the HUD is off.");
            return;
        }
        cef.RegisterFolder(Host, _folder);
        state.Meal += OnMeal;
        state.MealRefused += OnRefused;
        _key = input.RegisterKeyBind(ModifierKeys.Alt, Key.H, Toggle, KeySource.Polled);

        // Both created hidden, so the pages have long loaded before they are first shown.
        (_hudViewport, _hudPage) = Create(HudZOrder, HudPlacement, "hud.html");
        (_satchelViewport, _satchelPage) = Create(SatchelZOrder, ViewportPlacement.FullScreen, "satchel.html");
        logger.LogInformation("[HungerAndThirst] Pages loading (readym://{Host}/), the bars show once in the world.", Host);
    }

    private (ViewportHandle, BrowserHandle) Create(int zOrder, ViewportPlacement placement, string page)
    {
        ViewportHandle viewport = cef.CreateViewport(zOrder);
        cef.SetPlacement(viewport, placement);
        cef.SetFocusable(viewport, false);
        CefBrowserInstance browser = cef.CreateBrowser();
        browser.Navigate($"readym://{Host}/{page}");
        cef.Assign(viewport, browser.Handle);
        cef.SetVisible(viewport, false);
        return (viewport, browser.Handle);
    }

    private void Stop()
    {
        state.Meal -= OnMeal;
        state.MealRefused -= OnRefused;
        _key?.Dispose();
        if (_open)
            Guard("closing the satchel", () => SetOpen(false));
    }

    private void Update()
    {
        if (_hudPage is null)
            return;

        // Each time the character (re)appears, after the first spawn or a load, the overlay may be a new widget.
        bool inWorld = players.LocalMainCharacter is not null;
        if (inWorld && !_inWorld)
            _showAt = Now + ShowDelaySeconds;
        else if (!inWorld)
        {
            if (_inWorld)
                ForgetCursor();
            _showAt = double.PositiveInfinity;
            _hudArmed = false;
            _hudShown = false;
        }
        _inWorld = inWorld;
        if (Now >= _showAt)
        {
            _showAt = double.PositiveInfinity;
            _hudArmed = true;
            ForgetCursor();
            Guard("showing the bars", () => ShowHud(true, rearm: true));
        }

        // Out of the way, viewport and all, while a game menu is up: a collapsed viewport can't take the cursor from
        // the game's menus.
        string hiddenFor = HiddenFor;
        if (hiddenFor != _hiddenFor)
        {
            logger.LogInformation("[HungerAndThirst] Bars {State}.", hiddenFor.Length == 0 ? "back" : "hidden: " + hiddenFor);
            _hiddenFor = hiddenFor;
        }
        bool wanted = _hudArmed && hiddenFor.Length == 0;
        if (wanted != _hudShown)
            Guard("showing or hiding the bars", () => ShowHud(wanted, rearm: false));

        if (_message.Length > 0 && Now >= _messageUntil)
            _message = string.Empty;
        Push(force: false);
    }

    private bool HudHidden => HiddenFor.Length > 0;

    // Why the bars step aside right now, or empty when they don't.
    private string HiddenFor => !_hudEnabled ? "turned off by another mod"
        : !_open && _gameCursor ? "menu cursor"
        : string.Empty;

    // A load wipes any menu, and the pages may have reported a cursor (the loading screen's) that nobody will report
    // gone: start over, and let the pages report again.
    private void ForgetCursor()
    {
        _gameCursor = false;
        _cursorEpoch++;
    }

    private void Toggle()
    {
        Guard("Alt+H", () =>
        {
            if (!_open && AppDomain.CurrentDomain.GetData(TextInputKey) is true)
                return; // Alt+H while typing in another mod's page belongs to that page
            SetOpen(!_open);
        });
    }

    // Showing is what arms a viewport on the current overlay widget; hidden first when re-arming, so it forgets it
    // was ever shown.
    private void ShowHud(bool show, bool rearm)
    {
        if (rearm)
            cef.SetVisible(_hudViewport, false);
        cef.SetVisible(_hudViewport, show);
        _hudShown = show;
        if (rearm)
            logger.LogInformation("[HungerAndThirst] Bars shown.");
    }

    private void SetOpen(bool open)
    {
        if (_open == open || _satchelPage is not BrowserHandle page)
            return;
        _open = open;
        AppDomain.CurrentDomain.SetData(SatchelOpenKey, open);
        // Shown only while open (which also arms it, as RpRoll does on Home), and focusable only then, so with the
        // satchel closed it neither draws nor takes anything.
        cef.SetVisible(_satchelViewport, open);
        cef.SetFocusable(_satchelViewport, open);
        cef.SetFocused(_satchelViewport, open);
        cef.SetPageFocused(page, open); // a page that never had focus dispatches no keys
        Push(force: true);
    }

    // Only meals the player gave or got; other people's are left to whoever wants to show them.
    private void OnMeal(MealNews meal)
    {
        string what = meal.Food?.Name ?? "water";
        if (meal is { ByMe: true, ToMe: true })
            Say($"You have some {what}.");
        else if (meal.ByMe)
            Say($"You give them some {what}.");
        else if (meal.ToMe)
            Say($"You are given some {what}.");
    }

    private void OnRefused(MealRefusal reason) => Say(MealRefusals.Describe(reason));

    private void Say(string message)
    {
        _message = message;
        _messageUntil = Now + MessageSeconds;
    }

    private void Push(bool force)
    {
        try
        {
            string json = JsonSerializer.Serialize(new
            {
                ready = state.Ready,
                hudHidden = HudHidden,
                cursorEpoch = _cursorEpoch,
                satiety = Math.Round(state.Satiety, 1),
                hydration = Math.Round(state.Hydration, 1),
                satietyRate = state.SatietyRate,
                hydrationRate = state.HydrationRate,
                satchel = new
                {
                    open = _open,
                    water = _open && state.InWater,
                    message = _message,
                    // Only built while open: walking the inventory every frame for a closed satchel is wasted work.
                    items = _open
                        ? state.Carried().Select(carried => new
                        {
                            id = carried.Food.FormId,
                            name = carried.Food.Name,
                            count = carried.Count,
                            satiety = carried.Food.Satiety,
                            hydration = carried.Food.Hydration,
                        }).ToArray()
                        : [],
                },
            }, Json);
            if (!force && json == _lastJson && Now - _lastPush < KeepAliveSeconds)
                return;
            _lastJson = json;
            _lastPush = Now;
            _seq++;
            if (_hudPage is BrowserHandle hud)
                SendHatState(hud, _seq, json);
            if (_satchelPage is BrowserHandle satchel)
                SendHatState(satchel, _seq, json);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[HungerAndThirst] Sending the state to the pages failed.");
        }
    }

    private void Guard(string what, Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[HungerAndThirst] {What} failed.", what);
        }
    }

    // ---- game to page ----

    [SendToCef]
    public partial void SendHatState(BrowserHandle page, long seq, string json);

    // ---- page to game ----

    [ReceiveFromCef]
    private void OnHatReady(BrowserHandle page)
    {
        logger.LogInformation("[HungerAndThirst] A page loaded and is talking back.");
        Push(force: true);
    }

    /// <summary>The game started (1) or stopped (0) showing its mouse cursor, which it does exactly while one of its
    /// menus is open. Both pages report it; they see the same cursor. A report from before the last ForgetCursor is
    /// stale and ignored.</summary>
    [ReceiveFromCef]
    private void OnHatCursor(BrowserHandle page, long shown, long epoch)
    {
        if (epoch == _cursorEpoch)
            _gameCursor = shown != 0;
    }

    /// <summary>Enter on a satchel row: a form id, or 0 for "drink from the water".</summary>
    [ReceiveFromCef]
    private void OnHatUse(BrowserHandle page, long id)
    {
        Guard("using an item", () =>
        {
            if (!_open)
                return;
            if (id == 0)
                state.DrinkWater();
            else if (id is > 0 and <= uint.MaxValue)
                state.Eat((uint)id);
            Push(force: true);
        });
    }

    /// <summary>Esc in the satchel.</summary>
    [ReceiveFromCef]
    private void OnHatClose(BrowserHandle page)
    {
        Guard("Esc", () => SetOpen(false));
    }

    /// <summary>The open satchel lost the keyboard (a click elsewhere hands it to the game): take it back.</summary>
    [ReceiveFromCef]
    private void OnHatLostFocus(BrowserHandle page)
    {
        Guard("getting the keyboard back", () =>
        {
            if (!_open)
                return;
            cef.SetFocused(_satchelViewport, true);
            cef.SetPageFocused(page, true);
        });
    }
}
