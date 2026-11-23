using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Tests;

public sealed class PanelLinesTests
{
    private readonly PanelLines _lines = new();

    [Fact]
    public void Gains_of_one_item_add_up_on_one_line()
    {
        for (var i = 0; i < 6; i++)
            _lines.AddGain("1 Corn");

        Assert.Equal(["+6 Corn"], Texts());
    }

    [Fact]
    public void The_newest_line_is_on_top_and_an_item_gained_again_moves_up()
    {
        _lines.AddGain("1 Corn");
        _lines.AddGain("2 Firewood");
        _lines.AddGain("1 Corn");

        Assert.Equal(["+2 Corn", "+2 Firewood"], Texts());
    }

    [Fact]
    public void Past_the_most_shown_the_oldest_line_goes()
    {
        _lines.AddGain("1 Corn");
        _lines.AddGain("1 Wheat");
        _lines.AddGain("1 Lavender");
        _lines.Say("refusal", "Your axe is too dull to bite.");

        Assert.Equal(PanelLines.MostShown, _lines.Lines.Count);
        Assert.Equal(["Your axe is too dull to bite.", "+1 Lavender", "+1 Wheat"], Texts());
    }

    [Fact]
    public void A_line_said_again_moves_up_once_and_a_gain_with_no_count_stays_as_worded()
    {
        _lines.Say("note", "Too far.");
        _lines.AddGain("Firewood");
        _lines.Say("note", "Too far.");

        Assert.Equal(["Too far.", "+Firewood"], Texts());
        Assert.Null(_lines.Lines[1].Gain);
    }

    [Theory]
    [InlineData("6 Firewood", 6, "Firewood")]
    [InlineData("1 Unknown herb", 1, "Unknown herb")]
    public void A_gain_reads_its_count_and_its_item(string gain, int count, string item)
    {
        Assert.Equal(new PanelGain(count, item), PanelGain.TryParse(gain));
    }

    [Theory]
    [InlineData("Firewood")]
    [InlineData("-1 Corn")]
    [InlineData("3 ")]
    public void Wording_without_a_count_and_an_item_is_no_gain(string gain)
    {
        Assert.Null(PanelGain.TryParse(gain));
    }

    private string[] Texts() => _lines.Lines.Select(line => line.Text).ToArray();
}
