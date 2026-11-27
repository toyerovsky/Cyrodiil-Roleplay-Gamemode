using CyrodiilRP.Server.Players;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Progression;

[Service]
internal sealed partial class ProgressionRegistry
{
    private readonly Dictionary<OnlinePlayer, ProgressionState> _states = [];

    public ProgressionState? Find(OnlinePlayer player) =>
        _states.TryGetValue(player, out var state) && player.Character?.Id == state.CharacterId ? state : null;

    public int LevelOf(OnlinePlayer player) => Find(player)?.Level ?? ZoneMinimum;

    public void Set(OnlinePlayer player, ProgressionState state) => _states[player] = state;

    public void Remove(OnlinePlayer player) => _states.Remove(player);

    private const int ZoneMinimum = 1;
}
