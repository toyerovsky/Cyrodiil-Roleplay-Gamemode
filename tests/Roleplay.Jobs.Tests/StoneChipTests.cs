using System.Numerics;
using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Tests;

public class StoneChipTests
{
    private static readonly Vector3 Hit = new(1000f, 2000f, 300f);
    // The miner stands 200 cm to the hit's -X side, on ground at 250
    private static readonly Vector2 Miner = new(800f, 2000f);
    private const float Ground = 250f;

    [Fact]
    public void Thrown_straight_it_flies_back_towards_the_miner_and_up()
    {
        var chip = StoneChip.Thrown(Hit, Miner, 400f, spread: 0.5f, lift: 0f, spin: 0f, Ground);

        Assert.True(chip.Velocity.X < 0f, $"{chip.Velocity}");
        Assert.Equal(0f, chip.Velocity.Y, 0.001f);
        Assert.Equal(MathF.Sin(MathF.PI / 6f) * 400f, chip.Velocity.Z, 0.01f); // 30 degrees up at the lowest lift
        Assert.Equal(400f, chip.Velocity.Length(), 0.01f);
    }

    [Fact]
    public void It_starts_off_the_hit_on_the_miners_side()
    {
        var chip = StoneChip.Thrown(Hit, Miner, 400f, 0.5f, 0.5f, 0f, Ground);

        Assert.Equal(new Vector3(970f, 2000f, 320f), chip.Start);
    }

    [Fact]
    public void A_hit_below_the_ground_still_throws_from_the_ground()
    {
        var chip = StoneChip.Thrown(Hit with { Z = 100f }, Miner, 400f, 0.5f, 0.5f, 0f, Ground);

        Assert.Equal(Ground, chip.Start.Z);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(1f)]
    public void The_spread_sends_it_out_to_either_side_no_further_than_60_degrees(float spread)
    {
        var chip = StoneChip.Thrown(Hit, Miner, 400f, spread, 0f, 0f, Ground);

        // 60 degrees off straight back towards the miner, who stands to the -X side
        var flat = new Vector2(chip.Velocity.X, chip.Velocity.Y);
        Assert.Equal(60f, MathF.Atan2(MathF.Abs(flat.Y), -flat.X) * 180f / MathF.PI, 0.01f);
        Assert.Equal(spread < 0.5f ? 1 : -1, MathF.Sign(flat.Y));
    }

    [Fact]
    public void It_flies_under_gravity_lands_on_the_ground_and_lies_still_there()
    {
        // Up at 490 cm/s from 245 cm above the ground: lands after 1 + sqrt(2) / 2... worked out below
        var chip = new StoneChip(new Vector3(0f, 0f, 245f), new Vector3(100f, 0f, 490f), new Vector3(360f, 0f, 0f), 0f);

        Assert.Equal(new Vector3(50f, 0f, 245f + 245f - 122.5f), chip.PositionAt(0.5f));
        var lands = (490f + MathF.Sqrt(490f * 490f + 2f * 980f * 245f)) / 980f;
        Assert.Equal(lands, chip.LandsAt, 0.0001f);
        Assert.Equal(0f, chip.PositionAt(lands).Z, 0.01f);
        Assert.Equal(chip.PositionAt(lands), chip.PositionAt(lands + 3f));
        Assert.Equal(chip.RotationAt(lands), chip.RotationAt(lands + 3f));
        Assert.Equal(new Vector3(180f, 0f, 0f), chip.RotationAt(0.5f));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.25f)]
    [InlineData(0.6f)]
    public void A_burst_sends_pieces_every_way_round_and_up(float around)
    {
        var center = new Vector3(0f, 0f, 100f);

        var piece = StoneChip.Burst(center, 500f, around, lift: 0.5f, spin: 0f, groundZ: 0f);

        var (sin, cos) = MathF.SinCos(around * 2f * MathF.PI);
        var flat = Vector2.Normalize(new Vector2(piece.Velocity.X, piece.Velocity.Y));
        Assert.True(Vector2.Distance(new Vector2(cos, sin), flat) < 1e-4f, $"{flat}");
        Assert.True(piece.Velocity.Z > 0f);
        Assert.Equal(500f, piece.Velocity.Length(), 0.01f);
        Assert.Equal(center, piece.Start);
    }

    [Fact]
    public void A_miner_on_top_of_the_hit_still_throws_it_somewhere()
    {
        var chip = StoneChip.Thrown(Hit, new Vector2(Hit.X, Hit.Y), 400f, 0.5f, 0f, 0f, Ground);

        Assert.True(float.IsFinite(chip.Velocity.X) && chip.Velocity.X != 0f);
    }
}
