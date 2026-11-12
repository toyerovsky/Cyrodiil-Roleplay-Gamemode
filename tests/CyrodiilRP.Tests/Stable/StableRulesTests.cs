using System.Numerics;
using CyrodiilRP.Common.Traders;
using CyrodiilRP.Data;
using CyrodiilRP.Server.Stable;
using ReadyM.Api.Idents;

namespace CyrodiilRP.Tests.Stable;

public sealed class StableRulesTests
{
    private const float Tolerance = 0.01f;

    private static readonly Vector3 Here = new(192634f, 31614f, 1130f);

    [Fact]
    public void Every_horse_stands_as_one_of_the_games_own_of_its_coat()
    {
        Assert.Equal(0x0001F11Eu, StableRules.FindBase(HorseKind.Paint));
        Assert.Equal(0x0001F11Du, StableRules.FindBase(HorseKind.Bay));
        Assert.Equal(0x00015B92u, StableRules.FindBase(HorseKind.Chestnut));
        Assert.Equal(0x0001F11Bu, StableRules.FindBase(HorseKind.White));
        Assert.Equal(0x0001F11Au, StableRules.FindBase(HorseKind.Black));
    }

    [Fact]
    public void Every_horse_has_a_name_of_its_own()
    {
        var kinds = Enum.GetValues<HorseKind>();

        Assert.Equal(kinds.Length, kinds.Select(kind => StableRules.FindName(kind).Key).Distinct().Count());
    }

    [Fact]
    public void A_horse_of_ours_is_marked_apart_from_the_modes_people()
    {
        Assert.NotEqual(TraderOrigin.SpawnerRefId, StableRules.HorseSpawnerRefId);
        Assert.NotEqual(TraderOrigin.BankerSpawnerRefId, StableRules.HorseSpawnerRefId);
        Assert.NotEqual(TraderOrigin.StablemasterSpawnerRefId, StableRules.HorseSpawnerRefId);
    }

    [Theory]
    [InlineData("L_Tamriel", true)]
    [InlineData("L_ICMarketDistrict", false)]
    [InlineData("L_Chorrol", false)]
    [InlineData("L_PersistentDungeon-2F7D4", false)]
    public void A_horse_comes_only_in_the_open_world(string area, bool open) =>
        Assert.Equal(open, StableRules.IsOpenWorld(new AreaId(area)));

    [Theory]
    [InlineData(0f)]
    [InlineData(90f)]
    [InlineData(-135f)]
    [InlineData(300f)]
    public void A_called_horse_stands_to_the_side_of_the_character_a_little_higher(float yaw)
    {
        var spot = StableRules.FindSpotBeside(Here, yaw);

        var radians = yaw * MathF.PI / 180f;
        var facing = new Vector2(MathF.Cos(radians), MathF.Sin(radians));
        var aside = new Vector2(spot.X - Here.X, spot.Y - Here.Y);
        Assert.Equal(StableRules.SideCentimetres, aside.Length(), Tolerance);
        Assert.Equal(0f, Vector2.Dot(facing, aside), 1f);
        Assert.Equal(Here.Z + StableRules.RaiseCentimetres, spot.Z, Tolerance);
    }

    [Fact]
    public void The_window_reads_back_every_horse_by_the_name_it_sent_and_nothing_else()
    {
        foreach (var kind in Enum.GetValues<HorseKind>())
        {
            Assert.True(StableRules.TryRead(kind.ToString(), out var read));
            Assert.Equal(kind, read);
        }

        Assert.False(StableRules.TryRead("Shadowmere", out _));
        Assert.False(StableRules.TryRead("bay", out _));
        Assert.False(StableRules.TryRead("1", out _));
        Assert.False(StableRules.TryRead("", out _));
    }
}
