using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Tests;

public sealed class TreeFilterTests
{
    private readonly TreeFilter _filter = new(["tree", " Pine "], ["shrub", "stump", "branch", "bark"]);

    [Theory]
    [InlineData("/Game/Art/Foliage/Tree/SM_TreeWillowOakFreeSU.SM_TreeWillowOakFreeSU", true)]
    [InlineData("/Game/Art/Foliage/SM_WhitePine.SM_WhitePine", true)]
    [InlineData("/Game/Art/Foliage/Tree/SM_ManiaTree09.SM_ManiaTree09", true)]
    [InlineData("/Game/Art/Foliage/TREES/SM_X.SM_X", false)]                          // a tree folder is not a tree
    [InlineData("/Game/Art/Foliage/Tree/SM_Dbush15.SM_Dbush15", false)]               // a bush in the tree folder
    [InlineData("/Game/Art/Architecture/Street/SM_SignPostCity.SM_SignPostCity", false)] // "Street" holds "tree"
    [InlineData("/Game/Art/Foliage/Tree/SM_Branches_TreeBark_02_A.SM_Branches_TreeBark_02_A", false)] // fallen branches
    [InlineData("/Game/Art/Foliage/Trees/SM_ShrubAzalea.SM_ShrubAzalea", false)]
    [InlineData("/Game/Art/Foliage/Trees/SM_TreeStump01.SM_TreeStump01", false)]
    [InlineData("/Game/Art/Rocks/SM_Boulder01.SM_Boulder01", false)]
    public void A_tree_is_a_mesh_name_with_a_tree_part_and_no_excluded_part_ignoring_case(string mesh, bool isTree)
    {
        Assert.Equal(isTree, _filter.IsTree(mesh));
    }

    [Fact]
    public void The_lists_cross_the_wire_unchanged()
    {
        var copy = new TreeFilter(
            TreeFilter.Split(TreeFilter.Join(_filter.TreeMeshes)),
            TreeFilter.Split(TreeFilter.Join(_filter.ExcludedMeshes)));

        Assert.Equal(["tree", "Pine"], copy.TreeMeshes);
        Assert.Equal(["shrub", "stump", "branch", "bark"], copy.ExcludedMeshes);
    }

    [Fact]
    public void Only_a_single_tree_part_narrows_the_search()
    {
        Assert.Null(_filter.SearchHint);
        Assert.Equal("tree", new TreeFilter(["tree"], []).SearchHint);
    }

    [Fact]
    public void A_part_holding_the_separator_is_refused()
    {
        var error = Assert.Throws<ArgumentException>(() => new TreeFilter(["oak|pine"], []));
        Assert.Contains("oak|pine", error.Message);
    }

    [Fact]
    public void Nothing_is_a_tree_without_tree_parts()
    {
        Assert.False(new TreeFilter([], []).IsTree("/Game/Trees/SM_Oak.SM_Oak"));
    }
}
