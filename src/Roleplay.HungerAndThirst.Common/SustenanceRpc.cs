using ReadyM.SDK.Attributes;

namespace Roleplay.HungerAndThirst.Common;

// What the server tells a client about hunger and thirst besides the meters (which replicate on the Sustenance mixin).
// Meals themselves are a networked event, MealEvent. Form ids travel as plain uints.
[RpcContracts]
public static partial class SustenanceRpc
{
    // Sent by the client once its mod is running. The server answers with Foods and DecayRates, so a client that
    // loads late never misses them.
    [ClientToServer] public static partial void Hello();

    // How fast the meters fall right now, in points per minute. Sent when it changes, so the client can count down
    // between updates.
    [ServerToClient] public static partial void DecayRates(float satietyPerMinute, float hydrationPerMinute);

    // The server's food table: for each form id, what it gives and what to call it, same order in every array. Then
    // the time between two meals, so the satchel can say "still chewing" without asking, and what a sip of water
    // gives.
    [ServerToClient] public static partial void Foods(uint[] formIds, int[] satiety, int[] hydration, string[] names, float cooldownSeconds, int waterHydration);
}
