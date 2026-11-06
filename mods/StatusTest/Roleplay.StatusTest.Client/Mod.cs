using Microsoft.Extensions.Logging;
using OblivionMP.SDK;
using OblivionMP.SDK.Common.Statuses;
using OblivionMpCSharpMod;
using ReadyM.SDK.Attributes;
using OblivionMpCSharpMod.Input;
using Roleplay.StatusTest.Common;

namespace Roleplay.StatusTest.Client;

// Numpad test keys (Num Lock on):
//   1-9  a different server status each, see StatusTestSlot
//   0    clear every server status       -  remove the last server status
//   *    list the server's statuses        .  this client's own status (Fortify Speed 50 for 20 s)
//   /    remove this client's own statuses
[ModEntry]
public sealed partial class Mod(IInput input, IStatuses statuses, IUserInterface ui, StatusTestClientRpc rpc, ILogger logger)
{
    private readonly List<StatusHandle> _local = [];

    private void Init()
    {
        Bind(Key.NUM_ONE, StatusTestSlot.FortifyHealth);
        Bind(Key.NUM_TWO, StatusTestSlot.FortifyFatigueTimed);
        Bind(Key.NUM_THREE, StatusTestSlot.DrainStrengthTimed);
        Bind(Key.NUM_FOUR, StatusTestSlot.DamageHealthTimed);
        Bind(Key.NUM_FIVE, StatusTestSlot.RestoreMagickaTimed);
        Bind(Key.NUM_SIX, StatusTestSlot.FortifyBlade);
        Bind(Key.NUM_SEVEN, StatusTestSlot.Starving);
        Bind(Key.NUM_EIGHT, StatusTestSlot.FeatherTimed);
        Bind(Key.NUM_NINE, StatusTestSlot.BurdenTimed);
        Bind(Key.NUM_ZERO, StatusTestSlot.ClearAll);
        Bind(Key.SUBTRACT, StatusTestSlot.RemoveLast);
        Bind(Key.MULTIPLY, StatusTestSlot.ListActive);

        input.RegisterKeyBind(Key.DECIMAL, () => Guard("local status", ApplyLocal));
        input.RegisterKeyBind(Key.DIVIDE, () => Guard("local removal", RemoveLocal));

        rpc.Reported += text => ui.ShowMessage($"[StatusTest] {text}", MessagePosition.TopLeft, 5f);

        logger.LogInformation("StatusTest: client half loaded. Numpad 1-9 apply server statuses, 0 clears, - removes the last, * lists, . and / test this client's own.");
    }

    private void Bind(Key key, StatusTestSlot slot)
        => input.RegisterKeyBind(key, () => Guard(slot.ToString(), () => rpc.SendRun((byte)slot)));

    private void ApplyLocal()
    {
        var handle = statuses.Apply(StatusEffect.Of(StatusKind.Fortify, Stat.Speed, 50).WithName("Local haste"), 20f);
        _local.Add(handle);
        ui.ShowMessage($"[StatusTest] this client applied Local haste (Fortify Speed 50, 20 s) as {handle}", MessagePosition.TopLeft, 5f);
    }

    private void RemoveLocal()
    {
        var removed = _local.Count(statuses.Remove);
        _local.Clear();
        ui.ShowMessage($"[StatusTest] this client removed {removed} of its own statuses; {statuses.Active().Count} active in all", MessagePosition.TopLeft, 5f);
    }

    private void Guard(string what, Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "StatusTest: {What} failed.", what);
            ui.ShowMessage($"[StatusTest] {what} failed: {exception.Message}", MessagePosition.TopLeft, 5f);
        }
    }
}
