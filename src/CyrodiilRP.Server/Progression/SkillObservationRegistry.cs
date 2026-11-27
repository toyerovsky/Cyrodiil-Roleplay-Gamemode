using CyrodiilRP.Server.Players;
using System.Collections.Immutable;

namespace CyrodiilRP.Server.Progression;

internal sealed class SkillObservationRegistry
{
    private readonly Dictionary<OnlinePlayer, SkillObservation> _observations = [];

    public bool IsStable(OnlinePlayer player, int[] values)
    {
        var stable = _observations.TryGetValue(player, out var before) && before.Values.SequenceEqual(values);
        _observations[player] = new SkillObservation(values.ToImmutableArray());
        return stable;
    }

    public void Remove(OnlinePlayer player) => _observations.Remove(player);

    private sealed record SkillObservation(ImmutableArray<int> Values);
}
