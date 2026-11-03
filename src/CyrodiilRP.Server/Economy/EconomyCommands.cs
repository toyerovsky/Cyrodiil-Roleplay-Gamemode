using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Interactions;
using CyrodiilRP.Server.Localization;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Economy;

[Service]
internal sealed partial class EconomyCommands(CommandRegistry commands, Wallet wallet, RpChat chat, SelfMenu self)
{
    private const string GoldOption = "self.gold";
    private const int GoldOrder = 50;

    private void Start()
    {
        commands.Add(new CommandSpec { Name = "gold", Usage = EconomyText.CmdGold, Category = CommandCategory.General, NeedsCharacter = true },
            ShowGold);

        self.Add(new SelfOption { Id = GoldOption, Label = EconomyText.OptionGold, Icon = WheelIcons.Coins, Order = GoldOrder, Run = p => commands.Run(p, "/gold", chat) });
    }

    private void ShowGold(CommandCall call)
    {
        if (wallet.TryCount(call.Sender.Id, out var gold))
            call.Reply(EconomyText.Carrying(gold));
        else
            call.Refuse(EconomyText.NotInWorld);
    }
}
