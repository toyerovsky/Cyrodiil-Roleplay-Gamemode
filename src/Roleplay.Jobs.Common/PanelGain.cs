using System.Globalization;

namespace Roleplay.Jobs.Common;

/// <summary>A gain the Jobs panel shows: how many of what, as the server words it ("6 Firewood").</summary>
public sealed record PanelGain(int Count, string Item)
{
    public string Text => $"+{Count.ToString(CultureInfo.InvariantCulture)} {Item}";

    /// <summary>Null for wording that does not start with a count.</summary>
    public static PanelGain? TryParse(string gain)
    {
        var space = gain.IndexOf(' ', StringComparison.Ordinal);
        if (space <= 0 || !int.TryParse(gain.AsSpan(0, space), NumberStyles.None, CultureInfo.InvariantCulture, out var count))
            return null;

        var item = gain[(space + 1)..].Trim();
        return item.Length == 0 ? null : new PanelGain(count, item);
    }
}
