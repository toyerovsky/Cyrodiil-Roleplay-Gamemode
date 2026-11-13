using System.Diagnostics;
using System.Numerics;
using OblivionMP.SDK;
using OblivionMP.SDK.Common.Props;
using ReadyM.SDK.Attributes;
using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Client;

/// <summary>
/// Flies the pieces the jobs throw: stone chips and bursts off ore and rock, bark off a trunk. Each is a prop of this
/// client's own, with nothing colliding with it, moved along its <see cref="StoneChip"/> path every frame and taken
/// away when its time is up.
/// </summary>
[Service]
public sealed partial class ChipThrower(ILocalProps localProps)
{
    private sealed record Flying(LocalPropHandle Piece, StoneChip Path, double ThrownAt, float Seconds);

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly List<Flying> _flying = [];

    private double Now => _clock.Elapsed.TotalSeconds;

    /// <summary>Throws one of <paramref name="assets"/>, picked at random, along the path, for the given seconds.</summary>
    public void Throw(IReadOnlyList<string> assets, StoneChip path, float scale, float seconds)
    {
        if (assets.Count == 0 || seconds <= 0f)
            return;
        var asset = assets[Random.Shared.Next(assets.Count)];
        var piece = localProps.Place(new Prop(asset, path.Start, Vector3.Zero, scale), collides: false);
        _flying.Add(new Flying(piece, path, Now, seconds));
    }

    /// <summary>Throws <paramref name="count"/> chips off a hit at <paramref name="at"/>, back towards
    /// <paramref name="from"/> (who struck), starting no lower than <paramref name="groundZ"/>.</summary>
    public void ThrowOffHit(IReadOnlyList<string> assets, int count, Vector3 at, Vector2 from, float groundZ, float speed, float scale, float seconds)
    {
        for (var i = 0; i < count; i++)
        {
            var path = StoneChip.Thrown(at, from, speed * (0.7f + Random.Shared.NextSingle() * 0.6f),
                Random.Shared.NextSingle(), Random.Shared.NextSingle(), (Random.Shared.NextSingle() - 0.5f) * 1440f, groundZ);
            Throw(assets, path, scale, seconds);
        }
    }

    private void Update()
    {
        for (var i = _flying.Count - 1; i >= 0; i--)
        {
            var flying = _flying[i];
            var seconds = (float)(Now - flying.ThrownAt);
            if (seconds >= flying.Seconds)
            {
                localProps.Remove(flying.Piece);
                _flying.RemoveAt(i);
                continue;
            }
            localProps.Move(flying.Piece, flying.Path.PositionAt(seconds), flying.Path.RotationAt(seconds));
        }
    }

    private void Stop()
    {
        foreach (var flying in _flying)
            localProps.Remove(flying.Piece);
        _flying.Clear();
    }
}
