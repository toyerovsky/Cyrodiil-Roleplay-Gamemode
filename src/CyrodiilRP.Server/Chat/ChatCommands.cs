using CyrodiilRP.Common.Chat;
using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Interactions;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Players;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Chat;

[Service]
internal sealed partial class ChatCommands(
    CommandRegistry commands,
    RpChat chat,
    OnlinePlayers online,
    InteractionMenu interactions,
    RpConfig config)
{
    private const string GreetOption = "player.greet";
    private const string MessageOption = "player.pm";

    private const int GreetOrder = 10;
    private const int MessageOrder = 20;

    private void Start()
    {
        AddSpeech("me", ChatText.CmdMe, CommandCategory.Roleplay, Emote);
        AddSpeech("do", ChatText.CmdDo, CommandCategory.Roleplay, Do);
        AddSpeech("s", ChatText.CmdShout, CommandCategory.Roleplay, Shout, "shout");
        AddSpeech("w", ChatText.CmdWhisper, CommandCategory.Roleplay, Whisper, "whisper");
        AddSpeech("try", ChatText.CmdTry, CommandCategory.Roleplay, Try);
        AddSpeech("b", ChatText.CmdLocalOoc, CommandCategory.Messages, SayLocalOoc);
        AddSpeech("ooc", ChatText.CmdGlobalOoc, CommandCategory.Messages, SayGlobalOoc, "o");
        AddSpeech("pm", ChatText.CmdPm, CommandCategory.Messages, SendPrivate);
        commands.Add(new CommandSpec { Name = "players", Usage = ChatText.CmdPlayers, Category = CommandCategory.General, Aliases = ["who"] },
            ListPlayers);

        interactions.Add(new InteractionOption { Id = GreetOption, Label = ChatText.OptionGreet, Icon = WheelIcons.Hand, Order = GreetOrder, Speaks = true, Run = Greet });
        interactions.Add(new InteractionOption
        {
            Id = MessageOption,
            Label = ChatText.OptionPm,
            Icon = WheelIcons.Letter,
            Order = MessageOrder,
            Speaks = true,
            Reach = config.Chat.Say,
            Run = (actor, target) => chat.Prefill(actor, $"/pm {target.Number} "),
        });
    }

    private void Greet(OnlinePlayer actor, OnlinePlayer target) =>
        chat.SendAround(actor, config.Chat.Emote, ChatLineKind.Action, ChatText.Greet(new ShownName(actor), new ShownName(target)));

    // Out of character too: a mute is held by the character.
    private void AddSpeech(string name, Phrase usage, CommandCategory category, CommandHandler run, params string[] aliases) =>
        commands.Add(new CommandSpec { Name = name, Usage = usage, Category = category, Aliases = aliases, Speaks = true }, run);

    private void Emote(CommandCall call)
    {
        if (!HasText(call, ChatText.ExampleMe))
            return;

        chat.SendAround(call.Sender, config.Chat.Emote, ChatLineKind.Action, ChatText.Me(new ShownName(call.Sender), call.Arguments));
        chat.ShowOverhead(call.Sender, config.Chat.Emote, call.Arguments);
    }

    private void Do(CommandCall call)
    {
        if (HasText(call, ChatText.ExampleDo))
            chat.SendAround(call.Sender, config.Chat.Emote, ChatLineKind.Ambient, ChatText.Do(call.Arguments, new ShownName(call.Sender)));
    }

    private void Shout(CommandCall call)
    {
        if (HasText(call, ChatText.ExampleShout))
            chat.SendAround(call.Sender, config.Chat.Shout, ChatLineKind.Shout, ChatText.Shout(new ShownName(call.Sender), call.Arguments));
    }

    private void Whisper(CommandCall call)
    {
        if (HasText(call, ChatText.ExampleWhisper))
            chat.SendAround(call.Sender, config.Chat.Whisper, ChatLineKind.Whisper, ChatText.Whisper(new ShownName(call.Sender), call.Arguments));
    }

    private void Try(CommandCall call)
    {
        if (!HasText(call, ChatText.ExampleTry))
            return;

        var text = Random.Shared.Next(2) == 0
            ? ChatText.TrySuccess(new ShownName(call.Sender), call.Arguments)
            : ChatText.TryFailure(new ShownName(call.Sender), call.Arguments);
        chat.SendAround(call.Sender, config.Chat.Emote, ChatLineKind.Attempt, text);
    }

    private void SayLocalOoc(CommandCall call)
    {
        if (HasText(call, ChatText.ExampleLocalOoc))
            chat.SendAround(call.Sender, config.Chat.Emote, ChatLineKind.LocalOoc,
                ChatText.LocalOoc(call.Sender.Number, call.Sender.OocName, call.Arguments));
    }

    private void SayGlobalOoc(CommandCall call)
    {
        if (HasText(call, ChatText.ExampleGlobalOoc))
            chat.Broadcast(ChatLineKind.GlobalOoc, ChatText.GlobalOoc(call.Sender.Number, call.Sender.OocName, call.Arguments));
    }

    private void SendPrivate(CommandCall call)
    {
        if (!call.TryTarget(0, out var target))
            return;

        var text = call.SkipWords(1);
        if (text.Length == 0)
        {
            call.Refuse(ChatText.PmEmpty);
            return;
        }

        chat.SendTo(target, ChatLineKind.Private, ChatText.PmFrom(call.Sender.Number, call.Sender.OocName, text));
        chat.SendTo(call.Sender, ChatLineKind.Private, ChatText.PmTo(target.Number, target.OocName, text));
    }

    private void ListPlayers(CommandCall call)
    {
        var entries = online.All
            .OrderBy(player => player.Number)
            .Select(player => chat.Translate(call.Sender, player.Character is null
                ? ChatText.PlayerEntryNew(player.Number, player.OocName)
                : ChatText.PlayerEntry(player.Number, new ShownName(player), player.OocName)))
            .ToList();

        call.Reply(ChatText.Players(entries.Count, string.Join(", ", entries)));
    }

    private static bool HasText(CommandCall call, Phrase example)
    {
        if (call.Arguments.Length > 0)
            return true;

        call.Refuse(ChatText.Empty(example));
        return false;
    }
}
