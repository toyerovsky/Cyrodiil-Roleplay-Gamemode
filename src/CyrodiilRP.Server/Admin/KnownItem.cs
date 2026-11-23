using ReadyM.Relay.Common.Oblivion;
using ReadyM.Relay.Common.Oblivion.ECS.Values;

namespace CyrodiilRP.Server.Admin;

// An item the mode can name. Id: a game item's editor id, a custom item's "custom:" and its key. Item: what inventories
// name it by, to move it on whole. Type: its record's, a custom item's that of the game item it copies.
internal sealed record KnownItem(string Id, NetworkFormId Item, string Name, string Type)
{
    public static KnownItem Of(GameItem item) => new(item.Id, new FormId(item.Form), item.Name, item.Type);
}
