using System.Text.Json;
using CyrodiilRP.Common.Players;
using Microsoft.Extensions.Logging;
using OblivionMp.Sdk;
using OblivionMp.Sdk.Archetypes.Mixins;
using OblivionMp.Sdk.Common.Archetypes;
using OblivionMp.Sdk.Values;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client.Nameplates;

// The names over the other players, in place of the platform's MarkerMod, which writes a name once, as its marker is
// made. The server sends each player the names they see (KnownNames): a character's name once introduced, otherwise its
// race. A player the server has not named yet shows nothing, so a name never shows before it should. Over a name, a
// while, what that player last did (/me).
[Service]
internal sealed partial class Nameplates(IPlayers players, IMarkers markers, RpcHandlers rpc, ILogger logger)
{
    // An action fades out over its last second.
    private const float FadeSeconds = 1f;

    private const float OpacityStep = 0.05f;

    // As MarkerMod: dimmer as the player hides, gone once well hidden.
    private const float CeilingOpacity = 0.25f;
    private const float CeilingConcealment = 0.1f;
    private const float ZeroConcealment = 0.6f;

    // An action ends with an empty line as tall as the name under it, so the two never cover each other, whatever the
    // distance. A no-break space, which a text block keeps.
    private const string UnderAction = "\n ";

    private const string ActionMark = "* ";

    private const float MillisecondsPerSecond = 1000f;

    // MarkerMod's blue for the players, the staff on duty warm, and the chat's colour of an action (--action).
    private static readonly MarkerColor PlayerColor = new(0.4f, 0.7f, 1f);
    private static readonly MarkerColor DutyColor = new(1f, 0.55f, 0.4f);
    private static readonly MarkerColor ActionColor = new(0.78f, 0.64f, 0.88f);

    private static readonly MarkerStyle NameStyle = MarkerStyle.Default with
    {
        Socket = "head",
        OutlineSize = 2,
        OutlineColor = new MarkerColor(0.05f, 0.04f, 0.03f),
    };

    // About thirty characters a line.
    private static readonly MarkerStyle ActionStyle = NameStyle with { MaxWidth = 380f };

    private readonly Dictionary<ushort, Plate> _plates = [];

    private readonly Dictionary<ushort, PendingAction> _actions = [];

    private readonly HashSet<ushort> _seen = [];

    private readonly List<ushort> _gone = [];

    // By the player's number, as the server last named them.
    private Dictionary<ushort, string> _names = [];

    private void Start()
    {
        rpc.OverheadShown += OnOverheadShown;
        rpc.KnownNamesChanged += OnKnownNames;
    }

    private void Stop()
    {
        rpc.OverheadShown -= OnOverheadShown;
        rpc.KnownNamesChanged -= OnKnownNames;
        foreach (var plate in _plates.Values)
            Destroy(plate);
        _plates.Clear();
        _actions.Clear();
    }

    // The newest replaces the one before, at the next frame. One for a player not drawn yet waits for them while it lasts.
    private void OnOverheadShown(int speaker, string text, int showMs)
    {
        if (speaker is >= ushort.MinValue and <= ushort.MaxValue)
            _actions[(ushort)speaker] = new PendingAction(ActionMark + text + UnderAction, Time.Elapsed + showMs / MillisecondsPerSecond);
    }

    // The server's own JSON: one that does not read is a bug, and the names stay as they were.
    private void OnKnownNames(string json)
    {
        Dictionary<string, string>? named;
        try
        {
            named = JsonSerializer.Deserialize(json, NameplateJson.Default.DictionaryStringString);
        }
        catch (JsonException exception)
        {
            logger.LogError(exception, "[CyrodiilRP] The names over the players' heads did not read.");
            return;
        }

        var names = new Dictionary<ushort, string>();
        foreach (var (number, name) in named ?? [])
        {
            if (!ushort.TryParse(number, out var parsed))
            {
                logger.LogError("[CyrodiilRP] The names over the players' heads hold {Number}, which is no player's number.", number);
                return;
            }

            names[parsed] = name;
        }

        _names = names;
    }

    private void Update()
    {
        _seen.Clear();
        var local = players.LocalPlayer?.PlayerId.RawValue;
        foreach (var character in players.AreaPlayers)
        {
            var number = character.PlayerId.RawValue;
            if (number == local)
                continue;

            var pawn = character.Pawn;
            if (pawn == IntPtr.Zero)
                continue;

            _seen.Add(number);
            if (_plates.TryGetValue(number, out var plate) && plate.Pawn != pawn)
            {
                Destroy(plate);
                _plates.Remove(number);
                plate = null;
            }

            if (!_names.TryGetValue(number, out var nickname))
            {
                if (plate is not null)
                {
                    Destroy(plate);
                    _plates.Remove(number);
                }

                continue;
            }

            if (plate is null)
            {
                var handle = markers.CreateMarker(character, nickname, ColorOf(nickname), NameStyle);
                if (handle.IsNone)
                    continue;

                plate = new Plate(pawn, handle, nickname);
                _plates[number] = plate;
            }
            else if (nickname != plate.Nickname)
            {
                plate.Nickname = nickname;
                markers.SetMarkerText(plate.Name, nickname, ColorOf(nickname));
            }

            if (_actions.TryGetValue(number, out var action) && !ReferenceEquals(plate.Shown, action))
                ShowAction(plate, character, action);

            Fade(number, plate, character.Hidden);
        }

        Forget();
    }

    private void ShowAction(Plate plate, MainCharacter character, PendingAction action)
    {
        if (plate.Action.IsNone)
            plate.Action = markers.CreateMarker(character, action.Text, ActionColor, ActionStyle);
        else
            markers.SetMarkerText(plate.Action, action.Text, ActionColor);

        plate.Shown = action;
        plate.ActionBucket = -1;
    }

    // The name follows how hidden the player is, the action that too and its own fade, and goes once it has faded.
    private void Fade(ushort number, Plate plate, float hidden)
    {
        var seen = ComputeOpacity(hidden);
        var nameBucket = ToBucket(seen);
        if (plate.NameBucket != nameBucket)
        {
            markers.SetMarkerOpacity(plate.Name, nameBucket * OpacityStep);
            plate.NameBucket = nameBucket;
        }

        if (plate.Action.IsNone || !_actions.TryGetValue(number, out var action))
            return;

        var left = action.Until - Time.Elapsed;
        if (left <= 0f)
        {
            markers.DestroyMarker(plate.Action);
            plate.Action = MarkerHandle.None;
            _actions.Remove(number);
            return;
        }

        var actionBucket = ToBucket(seen * Math.Min(1f, left / FadeSeconds));
        if (plate.ActionBucket != actionBucket)
        {
            markers.SetMarkerOpacity(plate.Action, actionBucket * OpacityStep);
            plate.ActionBucket = actionBucket;
        }
    }

    // Players gone from the area, and the actions of those who left before they could be drawn.
    private void Forget()
    {
        _gone.Clear();
        foreach (var number in _plates.Keys)
        {
            if (!_seen.Contains(number))
                _gone.Add(number);
        }

        foreach (var number in _gone)
        {
            Destroy(_plates[number]);
            _plates.Remove(number);
        }

        if (_actions.Count == 0)
            return;

        _gone.Clear();
        foreach (var (number, action) in _actions)
        {
            if (action.Until <= Time.Elapsed && !_plates.ContainsKey(number))
                _gone.Add(number);
        }

        foreach (var number in _gone)
            _actions.Remove(number);
    }

    private void Destroy(Plate plate)
    {
        markers.DestroyMarker(plate.Name);
        markers.DestroyMarker(plate.Action);
    }

    private static MarkerColor ColorOf(string nickname) => Nameplate.IsOnDuty(nickname) ? DutyColor : PlayerColor;

    private static int ToBucket(float opacity) => (int)MathF.Round(opacity / OpacityStep);

    // Hidden: 0 for a player in plain sight, 1 for one unseen.
    private static float ComputeOpacity(float hidden)
    {
        var concealment = Math.Clamp(hidden, 0f, 1f);
        if (concealment <= 0f)
            return 1f;

        if (concealment >= ZeroConcealment)
            return 0f;

        if (concealment <= CeilingConcealment)
            return 1f + (CeilingOpacity - 1f) * (concealment / CeilingConcealment);

        return CeilingOpacity * (1f - (concealment - CeilingConcealment) / (ZeroConcealment - CeilingConcealment));
    }

    private sealed class Plate(IntPtr pawn, MarkerHandle name, string nickname)
    {
        public IntPtr Pawn { get; } = pawn;

        public MarkerHandle Name { get; } = name;

        public string Nickname { get; set; } = nickname;

        public MarkerHandle Action { get; set; } = MarkerHandle.None;

        public PendingAction? Shown { get; set; }

        // The opacity steps last set, -1 before the first.
        public int NameBucket { get; set; } = -1;

        public int ActionBucket { get; set; } = -1;
    }

    private sealed record PendingAction(string Text, float Until);
}
