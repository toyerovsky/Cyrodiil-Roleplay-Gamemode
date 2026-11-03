using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Server.Interactions;

// OnPick gets the id of an item that was offered enabled.
internal sealed record WheelRequest(string Title, IReadOnlyList<WheelItemView> Items, Action<OnlinePlayer, string> OnPick);
