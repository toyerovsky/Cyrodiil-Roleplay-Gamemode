using Microsoft.Extensions.Logging;
using ReadyM.SDK.Attributes;
using Roleplay.HungerAndThirst.Common;

namespace Roleplay.HungerAndThirst.Client;

/// <summary>Something the server says can be eaten or drunk, and what it gives.</summary>
public sealed record Food(uint FormId, string Name, int Satiety, int Hydration);

// The client end of SustenanceRpc. SendHello, SendConsume and SendDrinkWater are generated here; the server's
// answers come back as events for SustenanceState. Nothing escapes from here into the SDK.
[RpcHandlersFor(typeof(SustenanceRpc))]
public sealed partial class SustenanceClientRpc(ILogger logger)
{
    // The food table, then the seconds between two meals.
    public event Action<IReadOnlyList<Food>, float>? FoodsReceived;

    // Points per minute: satiety, then hydration.
    public event Action<float, float>? RatesReceived;

    // requestId, then the meters after the meal.
    public event Action<long, int, int>? Consumed;

    public event Action<long, string>? Refused;

    partial void OnFoods(uint[] formIds, int[] satiety, int[] hydration, string[] names, float cooldownSeconds)
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
        Raise(() => FoodsReceived?.Invoke(foods, cooldownSeconds));
    }

    partial void OnDecayRates(float satietyPerMinute, float hydrationPerMinute)
    {
        Raise(() => RatesReceived?.Invoke(satietyPerMinute, hydrationPerMinute));
    }

    partial void OnConsumed(long requestId, int satiety, int hydration)
    {
        Raise(() => Consumed?.Invoke(requestId, satiety, hydration));
    }

    partial void OnConsumeRefused(long requestId, string reason)
    {
        logger.LogInformation("[HungerAndThirst] Meal #{RequestId} refused: {Reason}", requestId, reason);
        Raise(() => Refused?.Invoke(requestId, reason));
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
