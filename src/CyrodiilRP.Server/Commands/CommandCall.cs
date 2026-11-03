using System.Diagnostics.CodeAnalysis;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Players;
using CyrodiilRP.Server.Staff;

namespace CyrodiilRP.Server.Commands;

internal sealed class CommandCall(OnlinePlayer sender, string arguments, OnlinePlayers online, StaffRoles roles, ICommandOutput output)
{
    public const int MinReasonLength = 3;

    private string[]? _words;

    public OnlinePlayer Sender { get; } = sender;

    public string Arguments { get; } = arguments;

    public IReadOnlyList<string> Words => _words ??= Arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    public void Reply(Phrase text) => output.Reply(Sender, text);

    public void Refuse(Phrase text) => output.Refuse(Sender, text);

    public string SkipWords(int skip) => CommandLine.SkipWords(Arguments, skip);

    public bool TryTarget(int index, [NotNullWhen(true)] out OnlinePlayer? target)
    {
        target = null;
        if (index >= Words.Count || !CommandLine.TryPlayerNumber(Words[index], out var number))
        {
            Refuse(CommandsText.Target);
            return false;
        }

        if (!online.TryFind(number, out target))
        {
            Refuse(CommandsText.NoPlayer(number));
            return false;
        }

        return true;
    }

    // A player number, or a ReadyM id for an account that may be offline.
    public bool TryAccount(int index, [NotNullWhen(true)] out AccountTarget? target)
    {
        target = null;
        if (index < Words.Count && Guid.TryParse(Words[index], out var readyMId))
        {
            target = new AccountTarget(readyMId, online.TryFind(readyMId, out var player) ? player : null);
            return true;
        }

        if (index < Words.Count && CommandLine.TryPlayerNumber(Words[index], out _))
        {
            if (!TryTarget(index, out var named))
                return false;

            target = new AccountTarget(named.ReadyMId, named);
            return true;
        }

        Refuse(CommandsText.TargetAccount);
        return false;
    }

    public bool TryNumber(int index, out int value)
    {
        if (index < Words.Count && CommandLine.TryNumber(Words[index], out value))
            return true;

        value = 0;
        Refuse(CommandsText.Number);
        return false;
    }

    public bool TryMinutes(int index, out TimeSpan? duration)
    {
        duration = null;
        if (index < Words.Count && CommandLine.TryMinutes(Words[index], out duration))
            return true;

        Refuse(CommandsText.Minutes(CommandLine.MaxMinutes));
        return false;
    }

    public bool TryReason(int skip, out string reason)
    {
        reason = SkipWords(skip).Trim();
        if (reason.Length >= MinReasonLength)
            return true;

        Refuse(CommandsText.Reason(MinReasonLength));
        return false;
    }

    // Not before the target is loaded: its roles are not known until then.
    public bool MayPunish(OnlinePlayer target, bool self = false)
    {
        if (target.Id == Sender.Id)
        {
            if (!self)
                Refuse(CommandsText.NotYourself);

            return self;
        }

        if (!target.IsLoaded)
        {
            Refuse(CommandsText.TargetJoining(target.Name));
            return false;
        }

        return Outranks(target.Name, target.Roles);
    }

    public bool Outranks(string targetName, IEnumerable<string> targetRoles)
    {
        if (roles.Outranks(Sender, targetRoles))
            return true;

        Refuse(CommandsText.Outranked(targetName));
        return false;
    }
}
