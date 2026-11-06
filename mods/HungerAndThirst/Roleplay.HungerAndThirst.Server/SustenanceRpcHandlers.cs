using Microsoft.Extensions.Logging;
using ReadyM.Relay.Server.Sdk.Rpc;
using ReadyM.SDK.Attributes;
using Roleplay.HungerAndThirst.Common;

namespace Roleplay.HungerAndThirst.Server;

// The server end of SustenanceRpc: the food table and the rates for a client that says hello. Meals are MealEvent,
// handled by SustenanceService. Nothing here throws back into the SDK.
[RpcHandlersFor(typeof(SustenanceRpc))]
public sealed partial class SustenanceRpcHandlers(SustenanceService sustenance, FoodTable foods, SustenanceConfig config, ILogger logger)
{
    // A client is ready: make sure it is tracked, then hand it the food table and its current rates.
    partial void OnHello(RpcContext context)
    {
        Guard("hello", context, () =>
        {
            sustenance.Track(context.Sender);
            FoodItem[] all = foods.All.ToArray();
            SendFoods(context.Sender,
                all.Select(food => food.FormId.Value).ToArray(),
                all.Select(food => food.Satiety).ToArray(),
                all.Select(food => food.Hydration).ToArray(),
                all.Select(food => food.Name).ToArray(),
                config.ConsumeCooldownSeconds,
                config.WaterHydration);
            (float satiety, float hydration) = sustenance.RatesOf(context.Sender);
            SendDecayRates(context.Sender, satiety, hydration);
        });
    }

    private void Guard(string what, RpcContext context, Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "HungerAndThirst: {What} from {Sender} failed.", what, context.Sender);
        }
    }
}

// Connects SustenanceService to the generated sends, so the service doesn't depend on the RPC class.
[Service]
public sealed partial class SustenanceRpcWiring(SustenanceService sustenance, SustenanceRpcHandlers rpc)
{
    private void Start()
    {
        sustenance.RatesChanged += rpc.SendDecayRates;
    }

    private void Stop()
    {
        sustenance.RatesChanged -= rpc.SendDecayRates;
    }
}
