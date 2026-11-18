using System.Diagnostics;
using System.Numerics;
using Microsoft.Extensions.Logging;
using OblivionMP.SDK;
using OblivionMP.SDK.Archetypes.Mixins;
using OblivionMP.SDK.Common.Archetypes;
using OblivionMP.SDK.Values;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Client.Entities;
using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Client;

/// <summary>
/// Herbalism on this side: what the player makes of each plant, from the server's herb list and their Alchemy tier
/// (<see cref="HerbLore"/>). The crosshair names a known plant, shows "???" over one they recognise and nothing at all
/// over one they do not, without the game's pick-up outline either (<see cref="ICrosshair"/>), and with the server's
/// markers on, the same shows over the unpicked herbs around them. Refusals and a master's extra harvest go to the Jobs
/// panel.
/// </summary>
[Service]
public sealed partial class HerbsClient(
    IPlayers players,
    IEntities entities,
    IMarkers markers,
    ICrosshair crosshair,
    HerbsClientRpc rpc,
    JobsPanel panel,
    ILogger logger)
{
    public const string UnknownName = "???";

    private const double LookEverySeconds = 0.5;
    private const double CountEverySeconds = 10;

    private static readonly MarkerColor KnownColor = new(0.62f, 0.86f, 0.45f);
    private static readonly MarkerColor RecognisedColor = new(0.86f, 0.84f, 0.78f);

    private static readonly MarkerStyle Style = MarkerStyle.Default with
    {
        OutlineSize = 2,
        OutlineColor = new MarkerColor(0.08f, 0.05f, 0.03f),
    };

    private sealed record Shown(MarkerHandle Handle, IntPtr Pawn, string Text);

    private readonly Dictionary<uint, Shown> _shown = [];
    private readonly HashSet<uint> _seen = [];
    private readonly HashSet<uint> _renamed = [];
    // Plants whose outline is off, by ref, with the game object it was turned off on
    private readonly Dictionary<uint, IntPtr> _outlineHidden = [];
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private HerbRules? _rules;
    private HerbTier? _alchemy;
    private bool _askedForRules;
    private double _nextLook;
    private double _nextCount;

    /// <summary>What the player sees over a plant: its name, "???", or nothing.</summary>
    public static string? TextFor(HerbKnowledge knowledge, string name) => knowledge switch
    {
        HerbKnowledge.Unaware => null,
        HerbKnowledge.Recognised => UnknownName,
        HerbKnowledge.Known or HerbKnowledge.Mastered => name,
        _ => throw new ArgumentOutOfRangeException(nameof(knowledge), knowledge, "Unknown herb knowledge."),
    };

    private void Start()
    {
        rpc.RulesReceived += OnRules;
        rpc.AlchemyReceived += OnAlchemy;
        rpc.Refused += OnRefused;
        rpc.Gathered += OnGathered;
    }

    private void Stop()
    {
        rpc.RulesReceived -= OnRules;
        rpc.AlchemyReceived -= OnAlchemy;
        rpc.Refused -= OnRefused;
        rpc.Gathered -= OnGathered;
        Forget();
    }

    private void Update()
    {
        FollowConnection();
        if (_rules is null || _alchemy is null || _clock.Elapsed.TotalSeconds < _nextLook)
            return;
        _nextLook = _clock.Elapsed.TotalSeconds + LookEverySeconds;
        LookAround(_rules, _alchemy.Value);
    }

    // The list and the tier come with each connection: what an earlier server sent is dropped
    private void FollowConnection()
    {
        var connected = players.LocalMainCharacter is not null;
        if (connected == _askedForRules)
            return;

        _askedForRules = connected;
        Forget();
        if (connected)
            rpc.SendHerbsReady();
    }

    private void OnRules(HerbRules rules)
    {
        RestoreCrosshair();
        _rules = rules;
        logger.LogInformation("Jobs: {Count} herbs; markers {Markers} within {Distance:0} cm.", rules.Herbs.Count,
            rules.Markers ? "on" : "off", rules.MarkerDistance);
        ApplyCrosshair();
    }

    private void OnAlchemy(HerbTier tier)
    {
        _alchemy = tier;
        logger.LogInformation("Jobs: Alchemy reaches {Tier} for herbs.", tier);
        ApplyCrosshair();
        ClearMarkers();
    }

    private void OnRefused(string message) => panel.Say(PanelLineKind.Refusal, message);

    private void OnGathered(IReadOnlyList<string> gains, string message) => panel.Outcome(gains, message);

    private void ApplyCrosshair()
    {
        if (_rules is null || _alchemy is null)
            return;

        foreach (var (plant, herb) in _rules.Herbs)
        {
            var text = TextFor(_rules.Know(herb, _alchemy.Value), herb.Name);
            if (text is null)
                crosshair.Hide(plant);
            else
                crosshair.Rename(plant, text);
            _renamed.Add(plant);
        }
    }

    // Within MarkerDistance: a plant the player does not know at all loses the game's pick-up outline, and with markers
    // on, every other one gets its name or "???" over it.
    private void LookAround(HerbRules rules, HerbTier alchemy)
    {
        if (players.LocalMainCharacter is not { } local)
        {
            ClearMarkers();
            return;
        }

        var from = local.Position;
        var reach = rules.MarkerDistance * rules.MarkerDistance;
        var count = _clock.Elapsed.TotalSeconds >= _nextCount;
        int known = 0, near = 0, mapped = 0, herbs = 0;
        _seen.Clear();
        foreach (var plant in entities.Query<Flora>())
        {
            known++;
            var inReach = Vector3.DistanceSquared(from, plant.Position) <= reach;
            if (count && inReach)
            {
                near++;
                if (plant.Pawn != IntPtr.Zero)
                    mapped++;
                if (rules.Herbs.ContainsKey(plant.BaseId.FormId.Value))
                    herbs++;
            }

            if (plant.IsHarvested || !rules.Herbs.TryGetValue(plant.BaseId.FormId.Value, out var herb) || !inReach)
                continue;

            var refId = plant.RefId;
            var pawn = plant.Pawn;
            var text = TextFor(rules.Know(herb, alchemy), herb.Name);
            FollowOutline(plant, refId, pawn, hidden: text is null);
            if (text is null || !rules.Markers)
                continue;

            _seen.Add(refId);
            if (_shown.TryGetValue(refId, out var shown))
            {
                if (shown.Pawn == pawn && shown.Text == text)
                    continue;
                markers.DestroyMarker(shown.Handle);
                _shown.Remove(refId);
            }

            var color = text == UnknownName ? RecognisedColor : KnownColor;
            var handle = markers.CreateMarker(plant, text, color, Style);
            if (!handle.IsNone)
                _shown[refId] = new Shown(handle, pawn, text);
        }

        foreach (var refId in _shown.Keys.Where(refId => !_seen.Contains(refId)).ToList())
        {
            markers.DestroyMarker(_shown[refId].Handle);
            _shown.Remove(refId);
        }

        if (count)
        {
            _nextCount = _clock.Elapsed.TotalSeconds + CountEverySeconds;
            logger.LogInformation("Jobs: {Known} server plants known; {Near} within {Distance:0} cm, {Mapped} of them on a game object, {Herbs} in the herb list.",
                known, near, rules.MarkerDistance, mapped, herbs);
        }
    }

    private void FollowOutline(Flora plant, uint refId, IntPtr pawn, bool hidden)
    {
        var wasHidden = _outlineHidden.TryGetValue(refId, out var hiddenPawn);
        if (hidden && (!wasHidden || hiddenPawn != pawn) && pawn != IntPtr.Zero)
        {
            crosshair.SetOutlineHidden(plant, true);
            _outlineHidden[refId] = pawn;
        }
        else if (!hidden && wasHidden)
        {
            crosshair.SetOutlineHidden(plant, false);
            _outlineHidden.Remove(refId);
        }
    }

    private void Forget()
    {
        ClearMarkers();
        RestoreCrosshair();
        _outlineHidden.Clear();
        _rules = null;
        _alchemy = null;
    }

    private void ClearMarkers()
    {
        foreach (var shown in _shown.Values)
            markers.DestroyMarker(shown.Handle);
        _shown.Clear();
    }

    private void RestoreCrosshair()
    {
        foreach (var plant in _renamed)
            crosshair.Restore(plant);
        _renamed.Clear();
    }
}
