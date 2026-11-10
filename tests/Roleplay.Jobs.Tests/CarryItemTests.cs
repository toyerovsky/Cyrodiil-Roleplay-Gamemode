using Roleplay.Jobs.Server;

namespace Roleplay.Jobs.Tests;

public sealed class CarryItemTests
{
    private static readonly CarryFit Shoulder = new() { OffsetX = -8f, OffsetZ = 18f, Pitch = 50f };
    private static readonly CarryFit Running = new() { OffsetY = 4f, Roll = 90f };
    private static readonly CarryFit Own = new() { OffsetX = 1f };

    [Fact]
    public void A_kind_takes_its_categorys_weight_and_fits()
    {
        var item = Single(new CarryKind { Name = "Plank", Category = "Beam" });

        Assert.Equal(300, item.Weight);
        Assert.Equal(20, item.DrainSpeed);
        Assert.Same(Shoulder, item.Standing);
        Assert.Same(Running, item.Moving);
    }

    [Fact]
    public void What_a_kind_gives_wins_over_its_category()
    {
        var item = Single(new CarryKind { Name = "Twig", Category = "Beam", Weight = 2, DrainSpeed = 0, Standing = Own });

        Assert.Equal(2, item.Weight);
        Assert.Equal(0, item.DrainSpeed);
        Assert.Same(Own, item.Standing);
        Assert.Same(Running, item.Moving);
    }

    [Fact]
    public void Without_a_moving_fit_anywhere_the_standing_one_is_used_moving()
    {
        var config = Config(new CarryKind { Name = "Crate", Category = "Box", Standing = Own });

        Assert.Same(Own, CarryItem.AllOf(config).Single().Moving);
    }

    [Fact]
    public void A_kind_of_a_category_not_listed_is_refused()
    {
        var config = Config(new CarryKind { Name = "Anvil", Category = "Iron" });

        Assert.Contains("'Iron'", Assert.Throws<InvalidOperationException>(() => CarryItem.AllOf(config)).Message);
    }

    [Fact]
    public void Two_kinds_of_one_name_are_refused()
    {
        var config = Config(new CarryKind { Name = "Log", Category = "Beam" }, new CarryKind { Name = "Log", Category = "Box" });

        Assert.Contains("'Log'", Assert.Throws<InvalidOperationException>(() => CarryItem.AllOf(config)).Message);
    }

    [Fact]
    public void The_shipped_kinds_all_have_a_category()
    {
        var items = CarryItem.AllOf(new CarryConfig());

        Assert.Equal(["Barrel", "Crate", "Sack", "Log"], items.Select(item => item.Name));
        Assert.Equal(300, items.Single(item => item.Name == "Log").Weight);
    }

    private static CarryItem Single(CarryKind kind) => CarryItem.AllOf(Config(kind)).Single();

    private static CarryConfig Config(params CarryKind[] kinds) => new()
    {
        Categories =
        [
            new CarryCategory { Name = "Beam", Weight = 300, DrainSpeed = 20, Standing = Shoulder, Moving = Running },
            new CarryCategory { Name = "Box", Weight = 25 },
        ],
        Kinds = [.. kinds],
    };
}
