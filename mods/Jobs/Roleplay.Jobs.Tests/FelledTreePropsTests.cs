using System.Numerics;
using OblivionMp.Sdk.Common.Foliage;
using Roleplay.Jobs.Server;

namespace Roleplay.Jobs.Tests;

public sealed class FelledTreePropsTests
{
    private static readonly FoliageId Oak = new("/Game/Art/Foliage/Tree/SM_TreeEnglishOakForestFA.SM_TreeEnglishOakForestFA", 19117, -152973, 14471);
    private static readonly FoliageId ScotchPine = new("/Game/Art/Foliage/Tree/SM_TreeScotchPineFree.SM_TreeScotchPineFree", 0, 0, 0);
    private static readonly FoliageId Pine = new("/Game/Art/Foliage/Tree/SM_TreeWhitePineForest.SM_TreeWhitePineForest", 0, 0, 0);
    private static readonly FoliageId Yew = new("/Game/Art/Foliage/Tree/SM_TreeYewForest.SM_TreeYewForest", 0, 0, 0);

    private readonly WoodcuttingConfig _config = new();

    [Theory]
    [InlineData(0f, 0f, 100f, 0f, 0f)]       // chopper on -X of the tree: it falls towards +X
    [InlineData(0f, 0f, 0f, 100f, 90f)]      // chopper on -Y: falls towards +Y
    [InlineData(100f, 0f, 0f, 0f, 180f)]     // chopper on +X: falls towards -X
    [InlineData(0f, 100f, 0f, 0f, -90f)]
    public void A_tree_falls_away_from_whoever_chopped_it(float chopperX, float chopperY, float treeX, float treeY, float yaw)
    {
        Assert.Equal(yaw, FelledTreeProps.FallYaw(new Vector3(chopperX, chopperY, 0), new Vector3(treeX, treeY, 500)), 0.01f);
    }

    [Fact]
    public void The_stump_stands_on_the_trunk_sunk_and_sized_to_the_tree_with_the_same_turn_every_time()
    {
        var stump = FelledTreeProps.Stump(Oak, 1.5f, _config);

        Assert.Equal(_config.Stump, stump.Asset);
        Assert.Equal(new Vector3(19117, -152973, 14471 - 30), stump.Position);
        Assert.Equal(1.5f, stump.Scale);
        Assert.Equal(0f, stump.Rotation.X);
        Assert.Equal(0f, stump.Rotation.Z);
        Assert.InRange(stump.Rotation.Y, 0f, 359f);
        Assert.Equal(stump, FelledTreeProps.Stump(Oak, 1.5f, _config));
        Assert.NotEqual(stump.Rotation.Y, FelledTreeProps.Stump(Oak with { X = Oak.X + 1 }, 1.5f, _config).Rotation.Y);
    }

    [Fact]
    public void A_birch_stump_is_narrower_than_an_oaks_of_the_same_scale()
    {
        var birch = new FoliageId("/Game/Art/Foliage/Tree/SM_TreeSilverBirchForestSU.SM_TreeSilverBirchForestSU", 0, 0, 0);

        Assert.Equal(0.75f, FelledTreeProps.Stump(birch, 1.5f, _config).Scale, 0.0001f);
        Assert.Equal(1.5f, FelledTreeProps.Stump(Oak, 1.5f, _config).Scale, 0.0001f);
    }

    [Fact]
    public void Stump_ids_are_tied_to_the_tree()
    {
        Assert.NotEqual(FelledTreeProps.StumpId(Oak), FelledTreeProps.StumpId(Pine));
        Assert.Equal(FelledTreeProps.StumpId(Oak), FelledTreeProps.StumpId(Oak with { }));
    }
}
