using CyrodiilRP.Server;
using CyrodiilRP.Server.Zones;

namespace CyrodiilRP.Tests.Zones;

public sealed class ZoneProgressionTests
{
    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(5, 0, 4)]
    [InlineData(3, 4, 4)]
    [InlineData(10, 0, 6)]
    [InlineData(20, 0, 6)]
    public void Gradient_uses_radial_distance_rounding_and_a_clamped_radius(int x, int y, int expected)
    {
        var zone = new ZoneDefinition { Mode = ZoneMode.Gradient, MinLevel = 1, MaxLevel = 6, Origin = [0, 0], Radius = 10 };
        Assert.Equal(expected, ZoneLevels.ForCell(zone, new ZoneCell(default, default, x, y), 40));
    }

    [Theory]
    [InlineData(1, 1, 0)]
    [InlineData(3, 10, -8)]
    [InlineData(40, 10, 10)]
    public void Spawn_offsets_map_our_level_to_the_engine_scale(int level, int session, int expected) =>
        Assert.Equal(expected, ZoneLevels.Offset(level, session));

    [Theory]
    [InlineData(1, 1)]
    [InlineData(15, 1.1)]
    [InlineData(21, 1.25)]
    [InlineData(27, 1.4)]
    [InlineData(35, 1.6)]
    public void Unset_npc_strength_defaults_to_the_zones_band(int minimum, double expected)
    {
        var zone = new ZoneDefinition { MinLevel = minimum };
        Assert.Equal(expected, zone.NpcHealth);
        Assert.Equal(expected, zone.NpcDamage);
    }

    [Fact]
    public void Effective_health_stacks_the_band_rank_and_extra_players_while_damage_uses_no_group_bonus()
    {
        var settings = new ZoneSettings();
        var health = 1.25 * settings.EliteHealth;
        var damage = 1.25 * settings.EliteDamage;
        Assert.Equal(-10, ZoneLevels.Strength(-60, false, health, damage, 2, settings), 4);
        Assert.Equal(-18.75f, ZoneLevels.Strength(-10, true, health, damage, 6, settings), 4);
        Assert.Equal(10, ZoneLevels.Strength(10, false, health, damage, 6, settings));
        Assert.Equal(-30, ZoneLevels.Strength(ZoneLevels.Damage(-10, true, 20, 10, settings), true, 1, 1.6666666666666667, 1, settings), 4);
    }

    [Fact]
    public void Invalid_gradient_and_strength_settings_are_refused()
    {
        var zone = new ZoneDefinition { Id = "test", Name = new() { ["en"] = "Test", ["pl"] = "Test" }, Mode = ZoneMode.Gradient, Radius = 0 };
        var settings = new ZoneSettings { Entries = [zone] };
        Assert.Throws<InvalidOperationException>(settings.RequireValid);
        zone.Radius = 2;
        zone.NpcHealth = double.NaN;
        Assert.Throws<InvalidOperationException>(settings.RequireValid);
        Assert.Throws<InvalidOperationException>(() => new ZoneSettings { GroupHealthPerPlayer = -1 }.RequireValid());
    }
}
