#if DEBUG
using OblivionMP.SDK;
using OblivionMpCSharpMod.Input;
using ReadyM.Relay.Common.Oblivion.ECS.Values;
using ReadyM.Relay.Common.Oblivion.Items;
using ReadyM.SDK.Attributes;

namespace Roleplay.HungerAndThirst.Client;

// Debug builds only: Numpad 1 puts bread and ale in the pack, to try the meters without a trader.
[Service]
internal sealed partial class DebugLarder(IInput input, IPlayers players)
{
    private const int Pieces = 20;

    private void Start() => input.RegisterKeyBind(Key.NUM_ONE, () =>
    {
        players.LocalMainCharacter?.AddItems(new InventoryEntry(Items.Ingredients.Breadloaf, Pieces, 0));
        players.LocalMainCharacter?.AddItems(new InventoryEntry(Items.Potions.DrinkAle, Pieces, 0));
    });
}
#endif
