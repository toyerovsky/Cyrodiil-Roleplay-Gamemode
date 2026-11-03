using CyrodiilRP.Server.Admin;

namespace CyrodiilRP.Tests.Admin;

public sealed class CatalogLookupTests
{
    private static readonly GameItem Sword = new("WeapIronLongsword", 0xC0C, "Iron Longsword", "WEAP");
    private static readonly GameItem ArenaSword = new("ArenaIronLongsword", 0x977D1, "Iron Longsword", "WEAP");
    private static readonly GameItem Lockpick = new("Lockpick", 0xA, "Lockpick", "MISC");
    private static readonly GameItem Bread = new("Breadloaf", 0x33680, "Bread Loaf", "INGR");

    private static readonly GameItem[] All = [Sword, ArenaSword, Lockpick, Bread];

    [Fact]
    public void The_whole_id_names_one_whatever_its_letter_case()
    {
        Assert.Same(ArenaSword, Find("arenaironlongsword").One);
    }

    [Fact]
    public void A_whole_name_two_things_share_leaves_the_staff_to_choose()
    {
        var found = Find("iron longsword");

        Assert.Null(found.One);
        Assert.Equal([Sword, ArenaSword], found.Candidates);
    }

    [Fact]
    public void A_part_of_one_name_is_enough()
    {
        Assert.Same(Bread, Find("loaf").One);
    }

    [Fact]
    public void Nothing_matching_and_nothing_typed_find_nothing()
    {
        Assert.Empty(Find("daedric").Candidates);
        Assert.Empty(Find("  ").Candidates);
    }

    private static Found<GameItem> Find(string text) => CatalogLookup.Find(All, text, item => item.Id, item => item.Name);
}
