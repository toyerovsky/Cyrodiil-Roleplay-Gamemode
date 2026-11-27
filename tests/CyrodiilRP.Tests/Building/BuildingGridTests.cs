using CyrodiilRP.Common.Building;

namespace CyrodiilRP.Tests.Building;

public sealed class BuildingGridTests
{
    private const float Precision = 0.001f;

    [Theory]
    [InlineData(34f, 25f, 50f)]
    [InlineData(50f, 25f, 75f)]
    [InlineData(34f, -25f, 25f)]
    [InlineData(25f, -25f, 0f)]
    [InlineData(0f, -25f, -25f)]
    [InlineData(-34f, 25f, -25f)]
    [InlineData(34f, 0f, 34f)]
    public void A_step_goes_to_the_next_whole_step_in_its_direction(float value, float by, float expected)
    {
        Assert.Equal(expected, BuildingGrid.StepToward(value, 0f, by), Precision);
    }

    [Fact]
    public void A_value_a_rounding_short_of_a_step_counts_as_on_it()
    {
        Assert.Equal(75f, BuildingGrid.StepToward(49.9999f, 0f, 25f), Precision);
        Assert.Equal(25f, BuildingGrid.StepToward(50.0001f, 0f, -25f), Precision);
    }

    [Fact]
    public void Steps_count_from_the_origin()
    {
        // A plot's ground at 1003 cm: 10 cm up from 1017 is 1023, the next 10 above the ground
        Assert.Equal(1023f, BuildingGrid.StepToward(1017f, 1003f, 10f), Precision);
        Assert.Equal(1003f, BuildingGrid.StepToward(1013f, 1003f, -10f), Precision);
    }

    [Fact]
    public void An_arrow_brings_a_piece_off_the_grid_onto_the_next_line()
    {
        var tile = BuildingGrid.TileCentimetres;
        Assert.Equal(tile, BuildingGrid.StepToward(34f, 0f, tile), Precision);
        Assert.Equal(2f * tile, BuildingGrid.StepToward(tile, 0f, tile), Precision);
        Assert.Equal(0f, BuildingGrid.StepToward(34f, 0f, -tile), Precision);
    }
}
