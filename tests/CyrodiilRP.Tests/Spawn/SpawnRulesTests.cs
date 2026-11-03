using CyrodiilRP.Server.Admin;
using CyrodiilRP.Server.Spawn;
using ReadyM.Relay.Common.Oblivion;

namespace CyrodiilRP.Tests.Spawn;

public sealed class SpawnRulesTests
{
    private const uint InteriorCell = 0x0001A2B3;
    private const string OtherWorld = "SEWorld";

    [Fact]
    public void The_spawn_belongs_to_the_outside_world_where_new_characters_begin()
    {
        Assert.True(SpawnRules.Allows(GameSpace.Outside));
    }

    [Fact]
    public void An_interior_or_another_world_cannot_hold_the_spawn()
    {
        Assert.False(SpawnRules.Allows(OblivionAreaIds.BuildInterior(InteriorCell)));
        Assert.False(SpawnRules.Allows(OblivionAreaIds.BuildExterior(OtherWorld)));
    }
}
