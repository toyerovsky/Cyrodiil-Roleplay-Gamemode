namespace CyrodiilRP.Common.Players;

// The name over a player's head is the nickname the server writes: the character's name, after the staff's mark while
// on duty, which the client shows in a colour of its own.
public static class Nameplate
{
    public const string DutyMark = "[Staff] ";

    public static bool IsOnDuty(string nickname) => nickname.StartsWith(DutyMark, StringComparison.Ordinal);
}
