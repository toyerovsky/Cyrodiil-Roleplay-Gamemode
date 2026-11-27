using System.Numerics;
using CyrodiilRP.Common.Building;
using CyrodiilRP.Server.Admin;

namespace CyrodiilRP.Tests.Building;

public sealed class PlotBoxTests
{
    private const float Tile = BuildingGrid.TileCentimetres;

    // A piece 3 m long and 1 m wide, its origin at the middle of its base.
    private static readonly Vector3 LongMin = new(-150f, -50f, 0f);
    private static readonly Vector3 LongMax = new(150f, 50f, 200f);

    [Fact]
    public void A_tile_is_a_square_of_the_terrain_and_a_cell_is_32_of_them()
    {
        Assert.Equal(GameSpace.UnitsPerCell * GameSpace.CentimetresPerUnit, BuildingGrid.TilesPerCell * Tile, precision: 2);
        Assert.Equal(182.857f, Tile, precision: 2);
    }

    [Fact]
    public void The_first_tile_of_a_cell_starts_at_its_edge()
    {
        var edge = BuildingGrid.EdgeOf(BuildingGrid.TilesPerCell);

        Assert.Equal(BuildingGrid.TilesPerCell, BuildingGrid.TileOf(edge + 1f));
        Assert.Equal(BuildingGrid.TilesPerCell - 1, BuildingGrid.TileOf(edge - 1f));
        Assert.Equal(1, GameSpace.ToGame(new Vector3(edge + 1f, 0f, 0f)).X / GameSpace.UnitsPerCell, precision: 2);
        Assert.Equal(-1, BuildingGrid.TileOf(-1f));
    }

    [Fact]
    public void A_snap_rounds_to_the_nearest_step_and_none_leaves_the_point_as_it_is()
    {
        Assert.Equal(Tile, BuildingGrid.Snap(Tile * 1.4f, 1), precision: 3);
        Assert.Equal(Tile * 1.5f, BuildingGrid.Snap(Tile * 1.4f, 2), precision: 3);
        Assert.Equal(Tile * 1.5f, BuildingGrid.Snap(Tile * 1.4f, 4), precision: 3);
        Assert.Equal(Tile * 1.4f, BuildingGrid.Snap(Tile * 1.4f, 0), precision: 3);
    }

    [Fact]
    public void A_plot_marked_between_two_points_holds_the_tiles_of_both_whichever_comes_first()
    {
        var first = new Vector3(Tile * 5.5f, Tile * -2.2f, 30f);
        var second = new Vector3(Tile * 1.1f, Tile * 3.9f, 10f);

        var box = PlotBox.Between(first, second, 10f, 600f);

        Assert.Equal(new PlotBox(1, -3, 5, 7, 10f, 600f), box);
        Assert.Equal(box, PlotBox.Between(second, first, 10f, 600f));
    }

    [Fact]
    public void A_long_piece_fits_along_a_long_plot_and_not_turned_across_it()
    {
        var plot = new PlotBox(0, 0, 2, 1, 0f, 500f);
        var middle = new Vector3(Tile, Tile / 2f, 0f);

        Assert.Equal(PlotFit.Fits, plot.Judge(LongMin, LongMax, middle, 0f));
        Assert.Equal(PlotFit.Outside, plot.Judge(LongMin, LongMax, middle, 90f));
        Assert.Equal(PlotFit.Fits, plot.Judge(LongMin, LongMax, middle, 180f));
    }

    [Fact]
    public void A_piece_whose_corner_crosses_the_edge_does_not_fit()
    {
        var plot = new PlotBox(0, 0, 4, 4, 0f, 500f);

        Assert.Equal(PlotFit.Fits, plot.Judge(LongMin, LongMax, new Vector3(150f, 50f, 0f), 0f));
        Assert.Equal(PlotFit.Outside, plot.Judge(LongMin, LongMax, new Vector3(140f, 50f, 0f), 0f));
        Assert.Equal(PlotFit.Outside, plot.Judge(LongMin, LongMax, new Vector3(150f, 50f, 0f), 30f));
    }

    [Fact]
    public void Only_the_top_of_a_piece_is_held_to_the_height_so_it_may_sink_into_the_ground()
    {
        var plot = new PlotBox(0, 0, 4, 4, 100f, 300f);
        var spot = new Vector3(Tile * 2f, Tile * 2f, 0f);

        Assert.Equal(PlotFit.Fits, plot.Judge(LongMin, LongMax, spot with { Z = -500f }, 0f));
        Assert.Equal(PlotFit.Fits, plot.Judge(LongMin, LongMax, spot with { Z = 200f }, 0f));
        Assert.Equal(PlotFit.TooHigh, plot.Judge(LongMin, LongMax, spot with { Z = 210f }, 0f));
    }

    [Fact]
    public void Plots_side_by_side_do_not_overlap_and_one_tile_in_common_does()
    {
        var plot = new PlotBox(0, 0, 4, 4, 0f, 500f);

        Assert.False(plot.Overlaps(new PlotBox(4, 0, 4, 4, 0f, 500f)));
        Assert.False(plot.Overlaps(new PlotBox(0, -4, 4, 4, 0f, 500f)));
        Assert.True(plot.Overlaps(new PlotBox(3, 3, 4, 4, 0f, 500f)));
        Assert.True(plot.Overlaps(new PlotBox(1, 1, 1, 1, 0f, 500f)));
    }
}
