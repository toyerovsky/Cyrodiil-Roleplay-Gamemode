using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;
using OblivionMP.SDK;
using ReadyM.Cef;
using ReadyM.Cef.Rpc;
using ReadyM.SDK.Attributes;

namespace Roleplay.Jobs.Client;

/// <summary>
/// The Jobs panel (web\panel.html, plain HTML): a small half-transparent plate at the top right, 30% down the screen,
/// showing the tree, vein or quarry the player last hit, how far along it is, and what came of it. It shows on every
/// hit or outcome and fades a few seconds after the last; it replaces the messages Jobs once wrote in the top left.
/// </summary>
/// <remarks>
/// <para>Hosted as Hunger's bars are (SustenancePage): a viewport only as big as the panel, never focusable, so the
/// game keeps every key; hidden while one of the game's menus shows its cursor, since a visible viewport would take
/// that menu's clicks on its rectangle; shown a moment after the character appears and again after every load, which
/// arms it on the overlay widget of the moment. The page reports the cursor (panel.js, JobsCursor).</para>
/// <para>Game to page: the whole state as one JSON message whenever it changes and once a second.</para>
/// </remarks>
[Service]
public sealed partial class JobsPanel(CefHost cef, IPlayers players, ILogger logger)
{
    private const string Host = "jobs";
    // Above Hunger's bars (2), below its satchel (3): over the game, under any window
    private const int ZOrder = 2;
    private const double KeepAliveSeconds = 1.0;
    private const double ShowDelaySeconds = 1.0;
    // What is on the plate is dropped this long after the last change: the page has faded it by then
    private const double ForgetAfterSeconds = 7.0;

    /// <summary>78.5% to 98.5% across, 30% to 44% down: clear of the compass, the quest notes and the game's own
    /// messages. panel.css sizes the plate from this viewport's height.</summary>
    private static readonly ViewportPlacement Placement = new()
    {
        AnchorMinX = 0.785f,
        AnchorMaxX = 0.985f,
        AnchorMinY = 0.30f,
        AnchorMaxY = 0.44f,
    };

    private static readonly JsonSerializerOptions Json = new() { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };

    private sealed record Line(string Kind, string Text);

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly List<Line> _lines = [];
    private string? _folder;
    private ViewportHandle _viewport;
    private BrowserHandle? _page;
    private string _title = string.Empty;
    private int _damage;
    private int _health;
    private long _seq;
    private double _changedAt = double.NegativeInfinity;
    private bool _inWorld;
    private bool _armed;
    private bool _shown;
    private double _showAt = double.PositiveInfinity;
    private bool _gameCursor;
    private long _cursorEpoch;
    private string _lastJson = string.Empty;
    private double _lastPush = double.NegativeInfinity;
    private long _pushSeq;

    private double Now => _clock.Elapsed.TotalSeconds;

    /// <summary>The client mod folder's web\. Set by the entry point before any service starts.</summary>
    public void UseFolder(string folder) => _folder = folder;

    /// <summary>A counted hit: what was struck, and how much of its health it has taken. A new hit clears what the
    /// last outcome said.</summary>
    public void Progress(string title, int damage, int health)
    {
        _title = title;
        _damage = Math.Max(0, damage);
        _health = Math.Max(0, health);
        _lines.Clear();
        Changed();
    }

    /// <summary>What came of the work: one line for each thing gained, or the sentence when nothing was.</summary>
    public void Outcome(IReadOnlyList<string> gains, string sentence)
    {
        if (Stale)
            ClearWork();
        if (gains.Count > 0)
            _lines.AddRange(gains.Select(gain => new Line("gain", "+" + gain)));
        else if (sentence.Length > 0)
            _lines.Add(new Line("note", sentence));
        Changed();
    }

    /// <summary>A line on its own: a hint or a refusal. Shown under the work going on, or alone when there is none.</summary>
    public void Say(PanelLineKind kind, string text)
    {
        if (text.Length == 0)
            return;
        if (Stale)
            ClearWork();
        var name = kind switch
        {
            PanelLineKind.Gain => "gain",
            PanelLineKind.Note => "note",
            PanelLineKind.Refusal => "refusal",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a panel line kind."),
        };
        _lines.RemoveAll(line => line.Text == text);
        _lines.Add(new Line(name, text));
        Changed();
    }

    // What the plate showed has faded: a new line starts afresh rather than under an old title
    private bool Stale => Now - _changedAt > ForgetAfterSeconds;

    private void ClearWork()
    {
        _title = string.Empty;
        _damage = 0;
        _health = 0;
        _lines.Clear();
    }

    private void Changed()
    {
        _seq++;
        _changedAt = Now;
        Push(force: true);
    }

    private void Start()
    {
        if (_folder is null)
        {
            logger.LogError("Jobs: no web folder, the panel is off.");
            return;
        }
        cef.RegisterFolder(Host, _folder);
        _viewport = cef.CreateViewport(ZOrder);
        cef.SetPlacement(_viewport, Placement);
        cef.SetFocusable(_viewport, false);
        var browser = cef.CreateBrowser();
        browser.Navigate($"readym://{Host}/panel.html");
        cef.Assign(_viewport, browser.Handle);
        cef.SetVisible(_viewport, false);
        _page = browser.Handle;
        logger.LogInformation("Jobs: panel loading (readym://{Host}/panel.html), shown once in the world.", Host);
    }

    private void Update()
    {
        if (_page is null)
            return;

        // Each time the character (re)appears, after the first spawn or a load, the overlay may be a new widget
        var inWorld = players.LocalMainCharacter is not null;
        if (inWorld && !_inWorld)
            _showAt = Now + ShowDelaySeconds;
        else if (!inWorld)
        {
            if (_inWorld)
                ForgetCursor();
            _showAt = double.PositiveInfinity;
            _armed = false;
            _shown = false;
        }
        _inWorld = inWorld;
        if (Now >= _showAt)
        {
            _showAt = double.PositiveInfinity;
            _armed = true;
            ForgetCursor();
            Guard("showing the panel", () => Show(true, rearm: true));
        }

        var wanted = _armed && !_gameCursor;
        if (wanted != _shown)
            Guard("showing or hiding the panel", () => Show(wanted, rearm: false));

        if (_seq > 0 && Stale && (_title.Length > 0 || _lines.Count > 0))
            ClearWork();
        Push(force: false);
    }

    // Showing is what arms a viewport on the current overlay widget; hidden first when re-arming
    private void Show(bool show, bool rearm)
    {
        if (rearm)
            cef.SetVisible(_viewport, false);
        cef.SetVisible(_viewport, show);
        _shown = show;
    }

    // A load wipes any menu, and the page may have reported a cursor nobody will report gone: start over
    private void ForgetCursor()
    {
        _gameCursor = false;
        _cursorEpoch++;
    }

    private void Push(bool force)
    {
        if (_page is not BrowserHandle page)
            return;
        try
        {
            var json = JsonSerializer.Serialize(new
            {
                hidden = !_shown,
                cursorEpoch = _cursorEpoch,
                seq = _seq,
                title = _title,
                damage = _damage,
                health = _health,
                lines = _lines.Select(line => new { kind = line.Kind, text = line.Text }).ToArray(),
            }, Json);
            if (!force && json == _lastJson && Now - _lastPush < KeepAliveSeconds)
                return;
            _lastJson = json;
            _lastPush = Now;
            SendJobsState(page, ++_pushSeq, json);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Jobs: sending the panel its state failed.");
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
            logger.LogError(exception, "Jobs: {What} failed.", what);
        }
    }

    // ---- game to page ----

    [SendToCef]
    public partial void SendJobsState(BrowserHandle page, long seq, string json);

    // ---- page to game ----

    [ReceiveFromCef]
    private void OnJobsReady(BrowserHandle page)
    {
        logger.LogInformation("Jobs: the panel loaded and is talking back.");
        Push(force: true);
    }

    /// <summary>The game started (1) or stopped (0) showing its mouse cursor, which it does exactly while one of its
    /// menus is open. A report from before the last ForgetCursor is stale and ignored.</summary>
    [ReceiveFromCef]
    private void OnJobsCursor(BrowserHandle page, long shown, long epoch)
    {
        if (epoch == _cursorEpoch)
            _gameCursor = shown != 0;
    }
}
