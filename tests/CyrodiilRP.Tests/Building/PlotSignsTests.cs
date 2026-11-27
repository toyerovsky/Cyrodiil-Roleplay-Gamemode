using System.Numerics;
using CyrodiilRP.Common.Building;

namespace CyrodiilRP.Tests.Building;

public sealed class PlotSignsTests
{
    private const float Precision = 0.01f;

    // Tiles 0 to 9 along x and 0 to 3 along y
    private static readonly PlotBox Plot = new(0, 0, 10, 4, 100f, 1500f);

    [Fact]
    public void A_sign_stands_at_the_middle_of_its_side_a_little_outside_turned_away()
    {
        var low = PlotSigns.SpotOn(Plot, PlotEdge.LowY);
        Assert.Equal((Plot.MinX + Plot.MaxX) / 2f, low.X, Precision);
        Assert.Equal(Plot.MinY - PlotSigns.Outside, low.Y, Precision);
        Assert.Equal(-90f, PlotSigns.YawOf(PlotEdge.LowY));

        var high = PlotSigns.SpotOn(Plot, PlotEdge.HighX);
        Assert.Equal(Plot.MaxX + PlotSigns.Outside, high.X, Precision);
        Assert.Equal((Plot.MinY + Plot.MaxY) / 2f, high.Y, Precision);
        Assert.Equal(0f, PlotSigns.YawOf(PlotEdge.HighX));
    }

    [Fact]
    public void Turning_the_front_goes_round_every_side_and_back()
    {
        var edge = PlotEdge.LowY;
        var seen = new List<PlotEdge>();
        for (var turn = 0; turn < 4; turn++)
        {
            seen.Add(edge);
            edge = PlotSigns.Next(edge);
        }

        Assert.Equal(PlotEdge.LowY, edge);
        Assert.Equal(Enum.GetValues<PlotEdge>().Order(), seen.Order());
    }

    [Fact]
    public void The_front_first_faces_the_viewer()
    {
        var middle = new Vector2((Plot.MinX + Plot.MaxX) / 2f, (Plot.MinY + Plot.MaxY) / 2f);
        Assert.Equal(PlotEdge.LowY, PlotSigns.Facing(Plot, middle + new Vector2(100f, -2000f)));
        Assert.Equal(PlotEdge.HighX, PlotSigns.Facing(Plot, middle + new Vector2(3000f, 500f)));
        Assert.Equal(PlotEdge.HighY, PlotSigns.Facing(Plot, middle + new Vector2(0f, 900f)));
        Assert.Equal(PlotEdge.LowX, PlotSigns.Facing(Plot, middle + new Vector2(-900f, 100f)));
    }

    [Fact]
    public void A_sign_belongs_to_the_side_it_stands_at()
    {
        foreach (var edge in Enum.GetValues<PlotEdge>())
            Assert.Equal(edge, PlotSigns.EdgeOf(Plot, PlotSigns.SpotOn(Plot, edge) + new Vector2(5f, -5f)));
    }
}
