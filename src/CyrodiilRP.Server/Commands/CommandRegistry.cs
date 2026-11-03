using CyrodiilRP.Server.Conditions;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Players;
using CyrodiilRP.Server.Staff;
using Microsoft.Extensions.Logging;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Commands;

[Service]
internal sealed partial class CommandRegistry(
    OnlinePlayers online,
    StaffRoles roles,
    CharacterConditions conditions,
    ILogger logger)
{
    private readonly Dictionary<string, RegisteredCommand> _byName = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<RegisteredCommand> _commands = [];

    // A name taken twice is a bug, so it throws.
    public void Add(CommandSpec spec, CommandHandler run)
    {
        var command = new RegisteredCommand(spec, run);
        foreach (var key in spec.Aliases.Prepend(spec.Name))
        {
            if (!_byName.TryAdd(key, command))
                throw new ArgumentException($"/{key} is already registered.", nameof(spec));
        }

        if (spec.Restricted)
            roles.Declare(spec.Name);

        _commands.Add(command);
    }

    // Its name is its permission (StaffPermissions).
    public void AddStaff(string name, Phrase usage, CommandHandler run, bool offDuty = false, bool needsCharacter = false) =>
        Add(new CommandSpec { Name = name, Usage = usage, Category = CommandCategory.Staff, OffDuty = offDuty, NeedsCharacter = needsCharacter }, run);

    public IEnumerable<CommandSpec> ListAvailable(OnlinePlayer player) =>
        _commands.Select(c => c.Spec).Where(spec => !spec.Restricted || roles.Allows(player, spec.Name));

    public void Run(OnlinePlayer sender, string line, ICommandOutput output)
    {
        if (!CommandLine.TryParse(line, out var name, out var arguments) || !_byName.TryGetValue(name, out var command))
        {
            output.Refuse(sender, CommandsText.Unknown(name));
            return;
        }

        if (FindRefusal(command.Spec, name, sender) is { } refusal)
        {
            output.Refuse(sender, refusal);
            return;
        }

        try
        {
            command.Run(new CommandCall(sender, arguments, online, roles, output));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[CyrodiilRP] /{Command} failed for {Player}.", name, sender.Id);
            output.Refuse(sender, CommandsText.Failed(name));
        }
    }

    // Null when the player may speak: by a command, or by plain words in the chat.
    public Phrase? FindSpeechRefusal(OnlinePlayer speaker)
    {
        if (!speaker.IsLoaded)
            return ChatText.Loading;

        // A mute is held by the character, so a player without one could not be muted.
        if (speaker.Character is null)
            return ChatText.NeedCharacter;

        return conditions.CheckSpeech(speaker);
    }

    private Phrase? FindRefusal(CommandSpec spec, string typed, OnlinePlayer sender)
    {
        // Answered as unknown, so a command a player may not run is not admitted to exist.
        if (spec.Restricted && !roles.Allows(sender, spec.Name))
            return CommandsText.Unknown(typed);

        // Roles, conditions and character are known only once loaded, and a banned player never is.
        if (!sender.IsLoaded)
            return ChatText.Loading;

        if (spec.Restricted && !spec.OffDuty && !roles.Acts(sender))
            return StaffText.OffDuty;

        if (spec.Speaks)
            return FindSpeechRefusal(sender);

        return spec.NeedsCharacter && sender.Character is null ? ChatText.NeedCharacter : null;
    }
}
