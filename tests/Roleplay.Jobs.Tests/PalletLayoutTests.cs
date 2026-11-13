using System.Numerics;
using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Tests;

public sealed class PalletLayoutTests
{
    private const float Tolerance = 0.01f;
    private static readonly Vector2 Footprint = new(190f, 130f);
    // A board lying along its Y, its origin at the middle of its underside
    private static readonly Vector3 BoardMin = new(-10f, -50f, 0f);
    private static readonly Vector3 BoardMax = new(10f, 50f, 4f);

    private const float AllWood = 1f;
    private const float NoInset = 0f;

    private sealed record Box(Vector3 Min, Vector3 Max);

    [Fact]
    public void Nails_under_the_wood_go_into_the_floor_and_the_runners()
    {
        var pallet = PalletLayout.Of(Footprint, BoardMin, BoardMax, woodShare: 0.5f, NoInset) ?? throw new InvalidOperationException("No pallet.");

        var boxes = pallet.Pieces.Select(piece => BoxOf(piece, BoardMin, BoardMax)).ToList();
        var runners = boxes.Take(3).ToList();
        var deck = boxes.Skip(3).ToList();
        var runnerBox = runners[0].Max.Z - runners[0].Min.Z;

        // Half of each box is nails: the runners' wood starts at the floor, the deck's on the runners' wood
        Assert.All(runners, runner => Assert.Equal(-runnerBox / 2f, runner.Min.Z, 2));
        var runnerTop = runners[0].Max.Z;
        Assert.All(deck, board => Assert.Equal(runnerTop, board.Min.Z + (board.Max.Z - board.Min.Z) / 2f, 2));
        Assert.Equal(deck[0].Max.Z, pallet.Height, 2);
    }

    [Fact]
    public void The_outer_runners_lie_inset_from_the_sides()
    {
        var pallet = PalletLayout.Of(Footprint, BoardMin, BoardMax, AllWood, runnerInset: 0.2f) ?? throw new InvalidOperationException("No pallet.");

        var middles = pallet.Pieces.Take(3).Select(piece => BoxOf(piece, BoardMin, BoardMax)).Select(box => (box.Min.Y + box.Max.Y) / 2f).ToList();

        Assert.Equal(-Footprint.Y * 0.3f, middles[0], 2);
        Assert.Equal(0f, middles[1], 2);
        Assert.Equal(Footprint.Y * 0.3f, middles[2], 2);
    }

    [Fact]
    public void Runners_lie_on_the_floor_under_a_deck_of_boards()
    {
        var pallet = PalletLayout.Of(Footprint, BoardMin, BoardMax, AllWood, NoInset) ?? throw new InvalidOperationException("No pallet.");

        var boxes = pallet.Pieces.Select(piece => BoxOf(piece, BoardMin, BoardMax)).ToList();
        var runners = boxes.Where(box => MathF.Abs(box.Min.Z) < Tolerance).ToList();
        var deck = boxes.Except(runners).ToList();

        Assert.Equal(3, runners.Count);
        Assert.All(runners, runner => Assert.Equal(Footprint.X, runner.Max.X - runner.Min.X, 2));
        Assert.True(deck.Count >= 2);
        Assert.All(deck, board => Assert.Equal(Footprint.Y, board.Max.Y - board.Min.Y, 2));
        var runnerTop = runners[0].Max.Z;
        Assert.All(deck, board => Assert.Equal(runnerTop, board.Min.Z, 2));
        Assert.Equal(boxes.Max(box => box.Max.Z), pallet.Height, 2);
    }

    [Fact]
    public void The_pallet_covers_its_footprint_and_no_more()
    {
        var pallet = PalletLayout.Of(Footprint, BoardMin, BoardMax, AllWood, NoInset) ?? throw new InvalidOperationException("No pallet.");

        var boxes = pallet.Pieces.Select(piece => BoxOf(piece, BoardMin, BoardMax)).ToList();

        Assert.Equal(-Footprint.X / 2f, boxes.Min(box => box.Min.X), 2);
        Assert.Equal(Footprint.X / 2f, boxes.Max(box => box.Max.X), 2);
        Assert.Equal(-Footprint.Y / 2f, boxes.Min(box => box.Min.Y), 2);
        Assert.Equal(Footprint.Y / 2f, boxes.Max(box => box.Max.Y), 2);
    }

    [Fact]
    public void A_board_with_its_origin_in_its_middle_still_stands_on_the_floor()
    {
        var min = new Vector3(-50f, -10f, -2f);
        var max = new Vector3(50f, 10f, 2f);

        var pallet = PalletLayout.Of(Footprint, min, max, AllWood, NoInset) ?? throw new InvalidOperationException("No pallet.");

        Assert.Equal(0f, pallet.Pieces.Select(piece => BoxOf(piece, min, max)).Min(box => box.Min.Z), 2);
    }

    [Fact]
    public void Without_a_measured_board_there_is_no_pallet()
    {
        Assert.Null(PalletLayout.Of(Footprint, Vector3.Zero, Vector3.Zero, AllWood, NoInset));
        Assert.Null(PalletLayout.Of(Vector2.Zero, BoardMin, BoardMax, AllWood, NoInset));
    }

    // Where a piece's board ends up, along the deposit's axes, from its eight corners
    private static Box BoxOf(PalletPiece piece, Vector3 boardMin, Vector3 boardMax)
    {
        var radians = piece.Yaw * MathF.PI / 180f;
        var cos = MathF.Cos(radians);
        var sin = MathF.Sin(radians);
        var corners = Enumerable.Range(0, 8).Select(corner =>
        {
            var local = new Vector3(
                (corner & 1) == 0 ? boardMin.X : boardMax.X,
                (corner & 2) == 0 ? boardMin.Y : boardMax.Y,
                (corner & 4) == 0 ? boardMin.Z : boardMax.Z) * piece.Scale;
            return piece.Offset + new Vector3(local.X * cos - local.Y * sin, local.X * sin + local.Y * cos, local.Z);
        }).ToList();
        return new Box(
            new Vector3(corners.Min(at => at.X), corners.Min(at => at.Y), corners.Min(at => at.Z)),
            new Vector3(corners.Max(at => at.X), corners.Max(at => at.Y), corners.Max(at => at.Z)));
    }
}
