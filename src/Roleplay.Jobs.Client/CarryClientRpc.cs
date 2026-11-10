using System.Numerics;
using Microsoft.Extensions.Logging;
using OblivionMpCSharpMod.Input;
using ReadyM.SDK.Attributes;
using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Client;

// The client end of CarryRpc: the generated sends, the server's messages as events for CarryClient.
[RpcHandlersFor(typeof(CarryRpc))]
public sealed partial class CarryClientRpc(ILogger logger)
{
    public event Action<CarryRules>? RulesReceived;
    public event Action<CarryKindView>? KindReceived;
    public event Action<int, int>? CarryingStarted;
    public event Action<int>? CarryingStopped;
    public event Action<string>? Refused;

    partial void OnCarryRules(string key, bool placementAllowed, string holdAnimation, string holdAnimationFirstPerson, float movingAbove,
        float putDownAhead, float carryHeight, float maxDrop)
    {
        if (!Enum.TryParse<Key>(key, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
        {
            logger.LogWarning("Jobs: the server's carry key {Key} is no key; carrying is off on this client.", key);
            return;
        }
        Raise(nameof(OnCarryRules), () => RulesReceived?.Invoke(new CarryRules(parsed, placementAllowed, holdAnimation, holdAnimationFirstPerson,
            movingAbove, putDownAhead, carryHeight, maxDrop)));
    }

    partial void OnCarryKind(int index, string name, string asset, float scale, string socket,
        float offsetX, float offsetY, float offsetZ, float pitch, float yaw, float roll,
        float movingOffsetX, float movingOffsetY, float movingOffsetZ, float movingPitch, float movingYaw, float movingRoll)
        => Raise(nameof(OnCarryKind), () => KindReceived?.Invoke(new CarryKindView(index, name, asset, scale, socket,
            new CarryFitView(new Vector3(offsetX, offsetY, offsetZ), new Vector3(pitch, yaw, roll)),
            new CarryFitView(new Vector3(movingOffsetX, movingOffsetY, movingOffsetZ), new Vector3(movingPitch, movingYaw, movingRoll)))));

    partial void OnCarrying(int player, int kindIndex)
        => Raise(nameof(OnCarrying), () => CarryingStarted?.Invoke(player, kindIndex));

    partial void OnCarryStopped(int player)
        => Raise(nameof(OnCarryStopped), () => CarryingStopped?.Invoke(player));

    partial void OnCarryRefused(string message)
        => Raise(nameof(OnCarryRefused), () => Refused?.Invoke(message));

    private void Raise(string message, Action raise)
    {
        try
        {
            raise();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Jobs: handling the server's {Message} failed.", message);
        }
    }
}
