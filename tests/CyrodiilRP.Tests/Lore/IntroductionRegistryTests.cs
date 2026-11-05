using CyrodiilRP.Server.Lore;

namespace CyrodiilRP.Tests.Lore;

public sealed class IntroductionRegistryTests
{
    private const long Marius = 1;
    private const long Lucia = 2;
    private const long Rajirr = 3;

    private readonly IntroductionRegistry _known = new();

    [Fact]
    public void Each_direction_is_known_on_its_own()
    {
        _known.Add(new Introduction(Marius, Lucia));

        Assert.True(_known.Knows(Marius, Lucia));
        Assert.False(_known.Knows(Lucia, Marius));
        Assert.Equal([Marius], _known.ListKnowers(Lucia));
    }

    [Fact]
    public void A_forgetting_takes_one_direction_and_says_whether_there_was_one()
    {
        _known.Add(new Introduction(Marius, Lucia));
        _known.Add(new Introduction(Lucia, Marius));

        Assert.True(_known.Remove(new Introduction(Lucia, Marius)));
        Assert.False(_known.Remove(new Introduction(Lucia, Marius)));
        Assert.True(_known.Knows(Marius, Lucia));
    }

    [Fact]
    public void A_character_that_leaves_takes_both_directions_with_it_and_leaves_the_rest()
    {
        _known.Add(new Introduction(Marius, Lucia));
        _known.Add(new Introduction(Lucia, Marius));
        _known.Add(new Introduction(Rajirr, Lucia));

        _known.Drop(Marius);

        Assert.False(_known.Knows(Marius, Lucia));
        Assert.False(_known.Knows(Lucia, Marius));
        Assert.Equal([Rajirr], _known.ListKnowers(Lucia));
    }
}
