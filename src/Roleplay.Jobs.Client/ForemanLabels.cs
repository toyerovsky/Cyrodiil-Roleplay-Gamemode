using System.Diagnostics;
using OblivionMP.SDK;
using OblivionMP.SDK.Archetypes.Mixins;
using OblivionMP.SDK.Common.Archetypes;
using OblivionMP.SDK.Values;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Client.Entities;

namespace Roleplay.Jobs.Client;

/// <summary>
/// The game names a foreman after the person they are made from, so this puts the role over each one's head, by the
/// origin the server stands them with. A marker needs the person's pawn, which comes and goes with its cell, so they
/// are looked over every second, as the gamemode does for its traders.
/// </summary>
[Service]
public sealed partial class ForemanLabels(IEntities entities, IMarkers markers, DocksClientRpc rpc)
{
    private const double LookEverySeconds = 1;

    // The panel's yellow, outlined so it reads against the sky
    private static readonly MarkerColor Yellow = new(1f, 0.82f, 0.16f);

    private static readonly MarkerStyle Style = MarkerStyle.Default with
    {
        Socket = "head",
        OutlineSize = 2,
        OutlineColor = new MarkerColor(0.08f, 0.05f, 0.03f),
    };

    private sealed record Shown(MarkerHandle Handle, IntPtr Pawn);

    private readonly Dictionary<uint, string> _labels = [];
    private readonly Dictionary<uint, Shown> _shown = [];
    private readonly HashSet<uint> _seen = [];
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private double _nextLook;

    private void Start() => rpc.Labelled += OnLabelled;

    private void Stop()
    {
        rpc.Labelled -= OnLabelled;
        foreach (var shown in _shown.Values)
            markers.DestroyMarker(shown.Handle);
        _shown.Clear();
    }

    private void OnLabelled(int origin, string name) => _labels[unchecked((uint)origin)] = name;

    private void Update()
    {
        if (_labels.Count == 0 || _clock.Elapsed.TotalSeconds < _nextLook)
            return;
        _nextLook = _clock.Elapsed.TotalSeconds + LookEverySeconds;

        _seen.Clear();
        foreach (var npc in entities.Query<Npc>())
        {
            if (!_labels.TryGetValue(npc.SpawnerRefId, out var label))
                continue;

            var refId = npc.RefId;
            var pawn = npc.Pawn;
            _seen.Add(refId);
            if (_shown.TryGetValue(refId, out var shown))
            {
                if (shown.Pawn == pawn)
                    continue;
                markers.DestroyMarker(shown.Handle);
                _shown.Remove(refId);
            }

            var handle = markers.CreateMarker(npc, label, Yellow, Style);
            if (!handle.IsNone)
                _shown[refId] = new Shown(handle, pawn);
        }

        foreach (var refId in _shown.Keys.Where(refId => !_seen.Contains(refId)).ToList())
        {
            markers.DestroyMarker(_shown[refId].Handle);
            _shown.Remove(refId);
        }
    }
}
