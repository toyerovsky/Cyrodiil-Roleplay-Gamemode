namespace CyrodiilRP.Common.Players;

// The name over a player's head is the nickname the server writes: the character's name, after the staff's mark while
// on duty and the away mark while away, and with the typing mark after it while they write in the chat. The client
// shows a name on duty and an away one in colours of their own.
public static class Nameplate
{
    public const string DutyMark = "[Staff] ";

    public const string AwayMark = "[AFK] ";

    public const string TypingMark = " ...";

    public static string Compose(bool onDuty, bool away, bool typing, string name) =>
        (onDuty ? DutyMark : string.Empty) + (away ? AwayMark : string.Empty) + name + (typing ? TypingMark : string.Empty);

    public static bool IsOnDuty(string nickname) => nickname.StartsWith(DutyMark, StringComparison.Ordinal);

    public static bool IsAway(string nickname) =>
        (IsOnDuty(nickname) ? nickname[DutyMark.Length..] : nickname).StartsWith(AwayMark, StringComparison.Ordinal);
}
