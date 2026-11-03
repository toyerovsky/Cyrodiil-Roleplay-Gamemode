using ReadyM.Relay.Common.Oblivion;
using ReadyM.Relay.Common.Oblivion.ECS.Values;

namespace CyrodiilRP.Server.Characters;

// The owner's game wears what the server's first list marks worn, a player's new character and a trader alike.
internal static class WornClothes
{
    private const int OnePiece = 1;
    private const byte Worn = 1;

    public static InventoryEntry CreateEntry(FormId piece) => new(piece, OnePiece, Worn);
}
