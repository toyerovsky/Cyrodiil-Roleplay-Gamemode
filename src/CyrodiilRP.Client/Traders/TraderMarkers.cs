using OblivionMP.SDK;
using OblivionMP.SDK.Archetypes.Mixins;
using OblivionMP.SDK.Common.Archetypes;
using OblivionMP.SDK.Values;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Client.Entities;

namespace CyrodiilRP.Client.Traders;

// The game names a trader, a banker, a stablemaster, a mentor or the registrar after the person it is made from, so the mode puts its own label
// over each. A marker needs the person's pawn, which comes and goes with its cell, so they are looked over every second.
[Service]
internal sealed partial class TraderMarkers(IEntities entities, IMarkers markers, TraderLabelRegistry labels)
{
    private const float LookEverySeconds = 1f;

    // The pages' gold (--gold in ui/rp/tokens.css), outlined so it reads against the sky.
    private static readonly MarkerColor Gold = new(0.86f, 0.71f, 0.39f);

    private static readonly MarkerStyle Style = MarkerStyle.Default with
    {
        Socket = "head",
        OutlineSize = 2,
        OutlineColor = new MarkerColor(0.08f, 0.05f, 0.03f),
    };

    private readonly Dictionary<uint, Shown> _shown = [];

    private readonly HashSet<uint> _seen = [];

    private readonly List<uint> _gone = [];

    private float _lastLook = float.NegativeInfinity;

    private readonly record struct Shown(MarkerHandle Handle, IntPtr Pawn, uint Origin);

    private void Start() => labels.Changed += SetLabel;

    private void Stop()
    {
        labels.Changed -= SetLabel;
        foreach (var shown in _shown.Values)
            markers.DestroyMarker(shown.Handle);
        _shown.Clear();
    }

    private void SetLabel(uint origin, string label)
    {
        foreach (var shown in _shown.Values)
        {
            if (shown.Origin == origin)
                markers.SetMarkerText(shown.Handle, label, Gold);
        }
    }

    private void Update()
    {
        if (labels.IsEmpty || Time.Elapsed - _lastLook < LookEverySeconds)
            return;

        _lastLook = Time.Elapsed;
        _seen.Clear();
        foreach (var npc in entities.Query<Npc>())
        {
            var origin = npc.SpawnerRefId;
            if (labels.Find(origin) is not { } label)
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

            var handle = markers.CreateMarker(npc, label, Gold, Style);
            if (!handle.IsNone)
                _shown[refId] = new Shown(handle, pawn, origin);
        }

        _gone.Clear();
        foreach (var refId in _shown.Keys)
        {
            if (!_seen.Contains(refId))
                _gone.Add(refId);
        }

        foreach (var refId in _gone)
        {
            markers.DestroyMarker(_shown[refId].Handle);
            _shown.Remove(refId);
        }
    }
}
