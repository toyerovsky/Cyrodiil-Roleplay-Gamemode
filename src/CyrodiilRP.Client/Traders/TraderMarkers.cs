using CyrodiilRP.Common.Traders;
using OblivionMP.SDK;
using OblivionMP.SDK.Archetypes.Mixins;
using OblivionMP.SDK.Common.Archetypes;
using OblivionMP.SDK.Values;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Client.Entities;

namespace CyrodiilRP.Client.Traders;

// The game names a trader or a banker after the person it is made from, so the mode puts its own label over each. A
// marker needs the person's pawn, which comes and goes with its cell, so they are looked over every second.
[Service]
internal sealed partial class TraderMarkers(IEntities entities, IMarkers markers, RpcHandlers rpc)
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

    // The label of each kind by the origin the server stands it with, once the server has sent it.
    private readonly Dictionary<uint, string> _labels = [];

    private readonly Dictionary<uint, Shown> _shown = [];

    private readonly HashSet<uint> _seen = [];

    private readonly List<uint> _gone = [];

    private float _lastLook = float.NegativeInfinity;

    private readonly record struct Shown(MarkerHandle Handle, IntPtr Pawn, uint Origin);

    private void Start()
    {
        rpc.TraderLabelled += OnTraderLabelled;
        rpc.BankerLabelled += OnBankerLabelled;
    }

    private void Stop()
    {
        rpc.TraderLabelled -= OnTraderLabelled;
        rpc.BankerLabelled -= OnBankerLabelled;
        foreach (var shown in _shown.Values)
            markers.DestroyMarker(shown.Handle);
        _shown.Clear();
    }

    private void OnTraderLabelled(string label) => SetLabel(TraderOrigin.SpawnerRefId, label);

    private void OnBankerLabelled(string label) => SetLabel(TraderOrigin.BankerSpawnerRefId, label);

    private void SetLabel(uint origin, string label)
    {
        _labels[origin] = label;
        foreach (var shown in _shown.Values)
        {
            if (shown.Origin == origin)
                markers.SetMarkerText(shown.Handle, label, Gold);
        }
    }

    private void Update()
    {
        if (_labels.Count == 0 || Time.Elapsed - _lastLook < LookEverySeconds)
            return;

        _lastLook = Time.Elapsed;
        _seen.Clear();
        foreach (var npc in entities.Query<Npc>())
        {
            var origin = npc.SpawnerRefId;
            if (!_labels.TryGetValue(origin, out var label))
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
