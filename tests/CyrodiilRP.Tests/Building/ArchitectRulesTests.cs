using System.Numerics;
using System.Text.Json;
using CyrodiilRP.Common.Building;
using CyrodiilRP.Data;
using CyrodiilRP.Server;
using CyrodiilRP.Server.Building;

namespace CyrodiilRP.Tests.Building;

public sealed class ArchitectRulesTests
{
    private const float Tile = BuildingGrid.TileCentimetres;

    private static readonly BuildingSettings Settings = new();
    private static readonly PlotForm Form = new(PlotKind.Industrial, 2500, 50, 12);

    // A plot of 10 by 6 tiles from tile 3, 4, its ground at 120 cm and its sign at its first corner.
    private static PlotMarking Marking(int width = 10, int depth = 6, float signX = 3 * Tile, float signZ = 120f) =>
        new([3, 4, width, depth], [120f, signX, 4 * Tile, signZ, 45f], "{}");

    [Fact]
    public void A_plot_marked_on_the_grid_is_accepted_with_the_height_of_its_form()
    {
        var plan = ArchitectRules.Plan(Marking(), Form, Settings);

        Assert.Equal(MarkingVerdict.Accepted, plan.Verdict);
        Assert.Equal(new PlotBox(3, 4, 10, 6, 120f, 1200f), plan.Box);
        Assert.Equal(new Vector3(3 * Tile, 4 * Tile, 120f), plan.Sign);
        Assert.Equal(45f, plan.SignYaw);
        Assert.Equal(Form, plan.Form);
    }

    [Fact]
    public void A_side_longer_than_the_config_allows_is_too_big()
    {
        Assert.Equal(MarkingVerdict.Accepted, ArchitectRules.Plan(Marking(width: Settings.MostTiles), Form, Settings).Verdict);
        Assert.Equal(MarkingVerdict.TooBig, ArchitectRules.Plan(Marking(width: Settings.MostTiles + 1), Form, Settings).Verdict);
        Assert.Equal(MarkingVerdict.TooBig, ArchitectRules.Plan(Marking(depth: Settings.MostTiles + 1), Form, Settings).Verdict);
    }

    [Theory]
    [InlineData(-1, 0, 10)]
    [InlineData(ArchitectRules.MostGold + 1, 0, 10)]
    [InlineData(0, -1, 10)]
    [InlineData(0, 0, 0)]
    [InlineData(0, 0, ArchitectRules.MostHeightMetres + 1)]
    public void Gold_below_0_or_past_the_most_and_a_height_outside_its_limits_are_refused(int price, int weeklyTax, int heightMetres)
    {
        var form = new PlotForm(PlotKind.Residential, price, weeklyTax, heightMetres);

        Assert.Equal(MarkingVerdict.FormInvalid, ArchitectRules.Plan(Marking(), form, Settings).Verdict);
    }

    [Fact]
    public void A_kind_outside_the_list_is_refused()
    {
        var form = Form with { Kind = (PlotKind)int.MaxValue };

        Assert.Equal(MarkingVerdict.FormInvalid, ArchitectRules.Plan(Marking(), form, Settings).Verdict);
    }

    [Fact]
    public void The_page_writes_the_form_the_server_reads()
    {
        var form = JsonSerializer.Deserialize("""{"kind":"Industrial","price":2500,"weeklyTax":50,"heightMetres":12}""", ArchitectJson.Default.PlotForm);

        Assert.Equal(Form, form);
    }

    [Fact]
    public void A_sign_far_from_the_edge_or_from_the_ground_is_refused()
    {
        Assert.Equal(MarkingVerdict.Accepted, ArchitectRules.Plan(Marking(signX: 3 * Tile - BuildingRules.SignReach), Form, Settings).Verdict);
        Assert.Equal(MarkingVerdict.SignFar, ArchitectRules.Plan(Marking(signX: 3 * Tile - BuildingRules.SignReach - 1f), Form, Settings).Verdict);
        Assert.Equal(MarkingVerdict.SignFar, ArchitectRules.Plan(Marking(signZ: 120f + BuildingRules.GroundReach + 1f), Form, Settings).Verdict);
    }

    [Fact]
    public void Numbers_missing_or_not_numbers_are_a_malformed_plot()
    {
        Assert.Equal(MarkingVerdict.Malformed, ArchitectRules.Plan(new PlotMarking([3, 4, 10], [120f, 0f, 0f, 0f, 0f], "{}"), Form, Settings).Verdict);
        Assert.Equal(MarkingVerdict.Malformed, ArchitectRules.Plan(new PlotMarking([3, 4, 10, 6], [120f, 0f, 0f, 0f], "{}"), Form, Settings).Verdict);
        Assert.Equal(MarkingVerdict.Malformed, ArchitectRules.Plan(new PlotMarking([3, 4, 10, 6], [float.NaN, 0f, 0f, 0f, 0f], "{}"), Form, Settings).Verdict);
        Assert.Equal(MarkingVerdict.Malformed, ArchitectRules.Plan(Marking(width: 0), Form, Settings).Verdict);
    }

    [Fact]
    public void A_spot_is_four_numbers()
    {
        Assert.True(ArchitectRules.TryReadSpot([1f, 2f, 3f, 90f], out var position, out var yaw));
        Assert.Equal(new Vector3(1f, 2f, 3f), position);
        Assert.Equal(90f, yaw);

        Assert.False(ArchitectRules.TryReadSpot([1f, 2f, 3f], out _, out _));
        Assert.False(ArchitectRules.TryReadSpot([1f, 2f, float.PositiveInfinity, 0f], out _, out _));
    }

    [Fact]
    public void A_placement_reads_its_yaw_and_a_tilted_one_its_pitch_and_roll_too()
    {
        Assert.True(ArchitectRules.TryReadPlacement([1f, 2f, 3f, 90f], out var position, out var level));
        Assert.Equal(new Vector3(1f, 2f, 3f), position);
        Assert.Equal(new Vector3(0f, 90f, 0f), level);

        Assert.True(ArchitectRules.TryReadPlacement([1f, 2f, 3f, 90f, -90f, 45f], out _, out var tilted));
        Assert.Equal(new Vector3(-90f, 90f, 45f), tilted);

        Assert.False(ArchitectRules.TryReadPlacement([1f, 2f, 3f, 90f, 5f], out _, out _));
        Assert.False(ArchitectRules.TryReadPlacement([1f, 2f, 3f, float.NaN], out _, out _));
    }

    [Fact]
    public void A_piece_taken_apart_reads_as_catalogue_pieces_each_at_its_place_and_turn()
    {
        var pieces = new PieceRegistry(Settings.ListPieces());
        var wall = KitSettings.KeyOf("SM_brumahouselower01_Wall_A");
        var roof = KitSettings.KeyOf("SM_brumahouselower01_Roof_A");

        Assert.True(ArchitectRules.TryReadParts(new PieceBreak(7, [wall, roof], [1f, 2f, 3f, 0f, 90f, 0f, 4f, 5f, 6f, 10f, 180f, 5f]), pieces,
            out var parts, out var verdict));
        Assert.Equal(BreakVerdict.Accepted, verdict);
        Assert.Equal([wall, roof], parts.Select(part => part.Piece.Key));
        Assert.Equal(new Vector3(4f, 5f, 6f), parts[1].Position);
        Assert.Equal(new Vector3(10f, 180f, 5f), parts[1].Rotation);
    }

    [Fact]
    public void Parts_of_a_count_unlike_their_numbers_too_many_or_unknown_are_refused()
    {
        var pieces = new PieceRegistry(Settings.ListPieces());
        var wall = KitSettings.KeyOf("SM_brumahouselower01_Wall_A");
        var spot = new float[ArchitectRules.PartLength];

        Assert.False(ArchitectRules.TryReadParts(new PieceBreak(7, [wall], [1f, 2f]), pieces, out _, out var shortSpots));
        Assert.Equal(BreakVerdict.Malformed, shortSpots);
        Assert.False(ArchitectRules.TryReadParts(new PieceBreak(7, [], []), pieces, out _, out var none));
        Assert.Equal(BreakVerdict.Malformed, none);
        Assert.False(ArchitectRules.TryReadParts(new PieceBreak(7, [wall], [1f, 2f, float.NaN, 0f, 0f, 0f]), pieces, out _, out var notNumber));
        Assert.Equal(BreakVerdict.Malformed, notNumber);

        var many = Enumerable.Repeat(wall, ArchitectRules.MostParts + 1).ToArray();
        Assert.False(ArchitectRules.TryReadParts(new PieceBreak(7, many, new float[many.Length * ArchitectRules.PartLength]), pieces, out _, out var tooMany));
        Assert.Equal(BreakVerdict.TooMany, tooMany);

        Assert.False(ArchitectRules.TryReadParts(new PieceBreak(7, ["kit.nothing_like_it"], spot), pieces, out _, out var unknown));
        Assert.Equal(BreakVerdict.UnknownPart, unknown);
        // The game's own bed is one object of the game, never a part
        Assert.False(ArchitectRules.TryReadParts(new PieceBreak(7, ["furniture.bed"], spot), pieces, out _, out var furniture));
        Assert.Equal(BreakVerdict.UnknownPart, furniture);
    }

    [Fact]
    public void A_piece_nobody_measured_is_judged_by_its_origin_and_a_measured_one_by_its_box()
    {
        var plot = new PlotBox(0, 0, 2, 2, 0f, 300f);
        var unmeasured = new Piece("fence.post", "Fence post", PieceCategory.Fences, "/Game/Post.Post", 1f, PieceUse.Any, PieceObject.Mesh, 0, null, string.Empty);
        var measured = unmeasured with { Bounds = new PieceBounds(new Vector3(-50f, -50f, 0f), new Vector3(50f, 50f, 100f)) };
        var nearEdge = new Vector3(10f, Tile, 0f);

        Assert.Equal(PlotFit.Fits, ArchitectRules.Judge(plot, unmeasured, nearEdge, 0f));
        Assert.Equal(PlotFit.Outside, ArchitectRules.Judge(plot, measured, nearEdge, 0f));
        Assert.Equal(PlotFit.Outside, ArchitectRules.Judge(plot, unmeasured, nearEdge with { X = -1f }, 0f));
        Assert.Equal(PlotFit.TooHigh, ArchitectRules.Judge(plot, unmeasured, nearEdge with { Z = 301f }, 0f));
    }
}
