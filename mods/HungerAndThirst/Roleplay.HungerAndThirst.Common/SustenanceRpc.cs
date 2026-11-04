using ReadyM.Api.Multiplayer;
using ReadyM.SDK.Attributes;

namespace Roleplay.HungerAndThirst.Common;

// Every message between the satchel and the server. The client only names what it wants to eat or drink; the
// server checks it, takes the item and answers the sender with the new meters. Form ids travel as plain uints.
[RpcContracts]
public static partial class SustenanceRpc
{
    // Sent by the client once its mod is running. The server answers with Foods and DecayRates, so a client that
    // loads late never misses them. The meters themselves replicate on the Sustenance mixin.
    [ClientToServer] public static partial void Hello();

    // Eat or drink one of this item from the sender's inventory. requestId comes back in the answer.
    [ClientToServer] public static partial void Consume(long requestId, uint formId);

    // Drink from the water the sender is swimming in.
    [ClientToServer] public static partial void DrinkWater(long requestId);

    // The request went through. The meters as they are now, after the meal.
    [ServerToClient] public static partial void Consumed(long requestId, int satiety, int hydration);

    // No meal: not carried, not food, too soon, not in water. The reason is shown to the player as is.
    [ServerToClient] public static partial void ConsumeRefused(long requestId, string reason);

    // How fast the meters fall right now, in points per minute. Sent when it changes, so the client can count down
    // between updates.
    [ServerToClient] public static partial void DecayRates(float satietyPerMinute, float hydrationPerMinute);

    // The server's food table: for each form id, what it gives and what to call it, same order in every array. And
    // the time between two meals, so the satchel can say "still chewing" without asking.
    [ServerToClient] public static partial void Foods(uint[] formIds, int[] satiety, int[] hydration, string[] names, float cooldownSeconds);
}
