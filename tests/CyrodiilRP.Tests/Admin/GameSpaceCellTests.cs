using System.Numerics;
using CyrodiilRP.Server.Admin;
using ReadyM.Api.Idents;
using ReadyM.Relay.Common.Oblivion;

namespace CyrodiilRP.Tests.Admin;

public sealed class GameSpaceCellTests
{
    [Theory]
    [InlineData(-64633f, 91819f, "-16,22")]
    [InlineData(134790f, -22092f, "32,-6")]
    [InlineData(-1f, 4095f, "-1,0")]
    public void A_place_lies_in_the_exterior_cell_of_the_games_grid(float x, float y, string cell)
    {
        Assert.Equal(cell, GameSpace.ToExteriorCell(GameSpace.ToUnreal(x, y, 0f)).ToManaged());
    }

    [Fact]
    public void The_servers_centimetres_give_the_same_cell_as_the_games_save()
    {
        Assert.Equal("3,24", GameSpace.ToExteriorCell(new Vector3(18418f, -143362f, 13014f)).ToManaged());
    }

    [Fact]
    public void Cells_are_near_within_the_given_count_either_way()
    {
        Assert.True(GameSpace.AreNear(OblivionCellIds.Exterior(-16, 22), OblivionCellIds.Exterior(-15, 23), 1));
        Assert.False(GameSpace.AreNear(OblivionCellIds.Exterior(-16, 22), OblivionCellIds.Exterior(32, -6), 1));
    }

    [Fact]
    public void An_interior_is_near_no_cell()
    {
        Assert.False(GameSpace.AreNear(OblivionCellIds.Interior, OblivionCellIds.Interior, 1));
        Assert.False(GameSpace.AreNear(OblivionCellIds.Interior, new CellId("3,24"), 1));
    }
}
