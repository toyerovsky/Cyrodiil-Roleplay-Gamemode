using CyrodiilRP.Common.Traders;
using OblivionMp.Sdk;
using OblivionMp.Sdk.Archetypes.Mixins;
using OblivionMp.Sdk.Common.Archetypes;
using OblivionMp.Sdk.Values;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Client.Entities;

namespace CyrodiilRP.Client.Traders;

// The game names a trader after the person it is made from, so the mode puts its own label over it. A marker needs the
// trader's pawn, which comes and goes with its cell, so the traders are looked over every second.
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

    private readonly Dictionary<uint, Shown> _shown = [];

    private readonly HashSet<uint> _seen = [];

    private readonly List<uint> _gone = [];

    private string _label = "";

    private float _lastLook = float.NegativeInfinity;

    private readonly record struct Shown(MarkerHandle Handle, IntPtr Pawn);

    private void Start() => rpc.TraderLabelled += OnLabelled;

    private void Stop()
    {
        rpc.TraderLabelled -= OnLabelled;
        foreach (var shown in _shown.Values)
            markers.DestroyMarker(shown.Handle);
        _shown.Clear();
    }

    private void OnLabelled(string label)
    {
        _label = label;
        foreach (var shown in _shown.Values)
            markers.SetMarkerText(shown.Handle, label, Gold);
    }

    private void Update()
    {
        if (_label.Length == 0 || Time.Elapsed - _lastLook < LookEverySeconds)
            return;

        _lastLook = Time.Elapsed;
        _seen.Clear();
        foreach (var npc in entities.Query<Npc>())
        {
            if (npc.SpawnerRefId != TraderOrigin.SpawnerRefId)
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

            var handle = markers.CreateMarker(npc, _label, Gold, Style);
            if (!handle.IsNone)
                _shown[refId] = new Shown(handle, pawn);
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
