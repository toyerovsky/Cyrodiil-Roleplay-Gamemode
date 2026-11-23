namespace Roleplay.Jobs.Common;

/// <summary>
/// The lines under the Jobs panel's bar, the newest on top. A gain of an item already shown adds to its count, a line
/// already shown moves up, and past <see cref="MostShown"/> the oldest go, so the plate keeps to its place on the screen
/// however fast the gains come.
/// </summary>
public sealed class PanelLines
{
    public const int MostShown = 3;

    public const string GainKind = "gain";

    private readonly List<PanelLine> _lines = [];

    public IReadOnlyList<PanelLine> Lines => _lines;

    public void Clear() => _lines.Clear();

    public void AddGain(string gain)
    {
        if (PanelGain.TryParse(gain) is not { } parsed)
        {
            Say(GainKind, "+" + gain);
            return;
        }

        var shown = _lines.FindIndex(line => line.Gain is { } earlier && earlier.Item == parsed.Item);
        if (shown >= 0)
        {
            parsed = parsed with { Count = parsed.Count + _lines[shown].Gain!.Count };
            _lines.RemoveAt(shown);
        }

        Put(new PanelLine(GainKind, parsed.Text, parsed));
    }

    public void Say(string kind, string text)
    {
        _lines.RemoveAll(line => line.Text == text);
        Put(new PanelLine(kind, text, null));
    }

    private void Put(PanelLine line)
    {
        _lines.Insert(0, line);
        if (_lines.Count > MostShown)
            _lines.RemoveRange(MostShown, _lines.Count - MostShown);
    }
}
