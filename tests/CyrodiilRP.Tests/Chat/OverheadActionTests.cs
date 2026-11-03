using CyrodiilRP.Server.Chat;

namespace CyrodiilRP.Tests.Chat;

public sealed class OverheadActionTests
{
    private const string Short = "waves to Lucia";
    private const string Ellipsis = "…";
    private const string Word = "slowly ";

    [Fact]
    public void A_short_action_shows_whole_for_the_least_time()
    {
        Assert.Equal(Short, OverheadAction.Shorten($"  {Short} "));
        Assert.Equal(OverheadAction.LeastShown, OverheadAction.ComputeShowTime(Short));
    }

    [Fact]
    public void A_long_action_is_cut_between_words_and_shows_as_long_as_it_takes_to_read()
    {
        var text = string.Concat(Enumerable.Repeat(Word, OverheadAction.MaxLength));

        var shown = OverheadAction.Shorten(text);

        Assert.True(shown.Length <= OverheadAction.MaxLength);
        Assert.EndsWith("slowly" + Ellipsis, shown);
        Assert.Equal(OverheadAction.PerCharacter * shown.Length, OverheadAction.ComputeShowTime(shown));
        Assert.True(OverheadAction.ComputeShowTime(shown) > OverheadAction.LeastShown);
    }

    [Fact]
    public void A_cut_never_splits_a_surrogate_pair()
    {
        var text = new string('a', OverheadAction.MaxLength - Ellipsis.Length - 1) + string.Concat(Enumerable.Repeat("😀", 10));

        var shown = OverheadAction.Shorten(text);

        Assert.False(char.IsHighSurrogate(shown[^(Ellipsis.Length + 1)]));
    }
}
