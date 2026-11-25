using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Server.Fishing;

// The fishing of each character playing now, once read from the database.
internal sealed class AnglerProgressRegistry
{
    private readonly Dictionary<OnlinePlayer, AnglerProgress> _progress = [];

    // Null until the player's character's fishing is read, and for another character than theirs now.
    public AnglerProgress? Find(OnlinePlayer player) =>
        _progress.TryGetValue(player, out var progress) && player.Character?.Id == progress.CharacterId ? progress : null;

    public void Set(OnlinePlayer player, AnglerProgress progress) => _progress[player] = progress;

    public void Remove(OnlinePlayer player) => _progress.Remove(player);
}
