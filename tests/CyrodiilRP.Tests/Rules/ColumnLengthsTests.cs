using CyrodiilRP.Data;

namespace CyrodiilRP.Tests.Rules;

public sealed class ColumnLengthsTests
{
    private const int Length = 5;

    [Fact]
    public void A_text_that_fits_is_kept_whole()
    {
        Assert.Equal("abc", ColumnLengths.Cut("abc", Length));
    }

    [Fact]
    public void A_longer_text_is_cut_to_the_length()
    {
        Assert.Equal("abcde", ColumnLengths.Cut("abcdefgh", Length));
    }

    [Fact]
    public void A_cut_never_splits_a_surrogate_pair()
    {
        Assert.Equal("abcd", ColumnLengths.Cut("abcd\U0001F600", Length));
    }
}
