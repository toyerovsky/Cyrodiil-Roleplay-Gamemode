namespace CyrodiilRP.Server.Chat;

// An action (/me) over its player's head: the start of it, for as long as it takes to read. The chat keeps it whole.
internal static class OverheadAction
{
    // About four short lines over a head.
    public const int MaxLength = 120;

    public static readonly TimeSpan LeastShown = TimeSpan.FromSeconds(6);

    // A slow reader's pace, ten characters a second: the longest shows for twelve seconds.
    public static readonly TimeSpan PerCharacter = TimeSpan.FromMilliseconds(100);

    private const string Ellipsis = "…";

    // A cut goes back to the last space within this many characters, so no word is split.
    private const int WordReach = 20;

    public static string Shorten(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length <= MaxLength)
            return trimmed;

        var end = MaxLength - Ellipsis.Length;
        if (char.IsLowSurrogate(trimmed[end]))
            end--;

        var space = trimmed.LastIndexOf(' ', end, WordReach);
        return trimmed[..(space > 0 ? space : end)].TrimEnd() + Ellipsis;
    }

    public static TimeSpan ComputeShowTime(string shown)
    {
        var reading = PerCharacter * shown.Length;
        return reading < LeastShown ? LeastShown : reading;
    }
}
