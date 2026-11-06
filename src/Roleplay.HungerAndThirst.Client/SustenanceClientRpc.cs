using Microsoft.Extensions.Logging;
using ReadyM.SDK.Attributes;
using Roleplay.HungerAndThirst.Common;

namespace Roleplay.HungerAndThirst.Client;

// The client end of SustenanceRpc. SendHello is generated here; the server's answers come back as events for
// SustenanceState. Nothing escapes from here into the SDK.
[RpcHandlersFor(typeof(SustenanceRpc))]
public sealed partial class SustenanceClientRpc(ILogger logger)
{
    // The food table, the seconds between two meals, then what a sip of water gives.
    public event Action<IReadOnlyList<Food>, float, int>? FoodsReceived;

    // Points per minute: satiety, then hydration.
    public event Action<float, float>? RatesReceived;

    partial void OnFoods(uint[] formIds, int[] satiety, int[] hydration, string[] names, float cooldownSeconds, int waterHydration)
    {
        if (formIds.Length != satiety.Length || formIds.Length != hydration.Length || formIds.Length != names.Length)
        {
            logger.LogError("[HungerAndThirst] The server's food table doesn't line up, ignored.");
            return;
        }
        Food[] foods = new Food[formIds.Length];
        for (int i = 0; i < foods.Length; i++)
            foods[i] = new Food(formIds[i], names[i], satiety[i], hydration[i]);
        logger.LogInformation("[HungerAndThirst] {Count} foods and drinks from the server.", foods.Length);
        Raise(() => FoodsReceived?.Invoke(foods, cooldownSeconds, waterHydration));
    }

    partial void OnDecayRates(float satietyPerMinute, float hydrationPerMinute)
    {
        Raise(() => RatesReceived?.Invoke(satietyPerMinute, hydrationPerMinute));
    }

    private void Raise(Action raise)
    {
        try
        {
            raise();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[HungerAndThirst] Handling a message from the server failed.");
        }
    }
}
