using Microsoft.Extensions.Logging;
using OblivionMp.Sdk;
using ReadyM.Api.DI;
using ReadyM.Cef.Rpc;
using ReadyM.Relay.Common.Oblivion.Items;
using ReadyM.SDK.Attributes;

namespace Roleplay.HungerAndThirst.Client;

// The mod's entry point. Services and RPC handlers are found and created on their own; this tells the page where
// its files are and hands it to the CEF bridge.
[ModEntry]
public sealed partial class Mod(IDependencyContainer services, SustenancePage page, IInput input, IPlayers players, IItems items, ILogger logger)
{
    // Runs once when the mod loads.
    private void Init()
    {
        page.UseFolder(Path.Combine(ModDirectory, "web"));
        // Required in 1.0 for [SendToCef]/[ReceiveFromCef]: without it the page and the game never hear each other,
        // and nothing fails at build time (same as RpRoll).
        services.RegisterSingleton<ICefRpcHandler>(page);
        logger.LogInformation("[HungerAndThirst] Loaded. Alt+H opens the satchel.");

#if DEBUG
        input.RegisterKeyBind(ReadyM.Sdk.Common.Input.Key.NUM_ONE, () =>
        {
            players.LocalMainCharacter?.AddItems( new ReadyM.Relay.Common.Oblivion.ECS.Values.InventoryEntry(Items.Ingredients.Breadloaf, 20, 0));
            players.LocalMainCharacter?.AddItems( new ReadyM.Relay.Common.Oblivion.ECS.Values.InventoryEntry(Items.Potions.DrinkAle, 20, 0));
        });
#endif
    }
}
