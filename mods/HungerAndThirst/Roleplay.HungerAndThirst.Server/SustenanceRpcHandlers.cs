using Microsoft.Extensions.Logging;
using ReadyM.Relay.Server.Sdk.Rpc;
using ReadyM.SDK.Attributes;
using Roleplay.HungerAndThirst.Common;

namespace Roleplay.HungerAndThirst.Server;

// The server end of SustenanceRpc. Every request is checked by SustenanceService; the answer goes to the sender only.
// Nothing here throws back into the SDK.
[RpcHandlersFor(typeof(SustenanceRpc))]
public sealed partial class SustenanceRpcHandlers(SustenanceService sustenance, FoodTable foods, SustenanceConfig config, ILogger logger)
{
    private const string FailedMessage = "That didn't work. Try again.";

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
                config.ConsumeCooldownSeconds);
            (float satiety, float hydration) = sustenance.RatesOf(context.Sender);
            SendDecayRates(context.Sender, satiety, hydration);
        });
    }

    partial void OnConsume(RpcContext context, long requestId, uint formId)
    {
        Answer(context, requestId, "consume", () => (sustenance.TryConsume(context.Sender, formId, out string refusal), refusal));
    }

    partial void OnDrinkWater(RpcContext context, long requestId)
    {
        Answer(context, requestId, "drink water", () => (sustenance.TryDrinkWater(context.Sender, out string refusal), refusal));
    }

    private void Answer(RpcContext context, long requestId, string what, Func<(bool Done, string Refusal)> request)
    {
        Guard(what, context, () =>
        {
            (bool done, string refusal) = request();
            if (done && sustenance.TryGetShown(context.Sender, out int satiety, out int hydration))
                SendConsumed(context.Sender, requestId, satiety, hydration);
            else
                SendConsumeRefused(context.Sender, requestId, done ? FailedMessage : refusal);
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
