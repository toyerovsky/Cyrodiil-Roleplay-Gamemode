using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Localization;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Economy;

[Service]
internal sealed partial class EconomyCommands(CommandRegistry commands, Wallet wallet)
{
    private void Start()
    {
        commands.Add(new CommandSpec { Name = "gold", Usage = EconomyText.CmdGold, Category = CommandCategory.General, NeedsCharacter = true },
            ShowGold);
    }

    private void ShowGold(CommandCall call)
    {
        if (wallet.TryCount(call.Sender.Id, out var gold))
            call.Reply(EconomyText.Carrying(gold));
        else
            call.Refuse(EconomyText.NotInWorld);
    }
}
