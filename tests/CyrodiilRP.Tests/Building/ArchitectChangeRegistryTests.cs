using System.Numerics;
using CyrodiilRP.Server.Building;

namespace CyrodiilRP.Tests.Building;

public sealed class ArchitectChangeRegistryTests
{
    private const string Architect = "account:architect";
    private const string Other = "account:other";
    private const long Plot = 3;
    private const long OtherPlot = 4;

    [Fact]
    public void Changes_are_taken_back_newest_first_and_only_on_the_plot_built_on()
    {
        var changes = new ArchitectChangeRegistry();
        changes.Record(Architect, Change(ArchitectChangeKind.Placed, Plot, 1));
        changes.Record(Architect, Change(ArchitectChangeKind.Removed, OtherPlot, 2));
        changes.Record(Architect, Change(ArchitectChangeKind.Removed, Plot, 3));

        Assert.True(changes.TryTake(Architect, Plot, out var newest));
        Assert.Equal(3, newest.PieceId);
        Assert.True(changes.TryTake(Architect, Plot, out var older));
        Assert.Equal(1, older.PieceId);
        Assert.False(changes.TryTake(Architect, Plot, out _));
        Assert.False(changes.TryTake(Other, OtherPlot, out _));
        Assert.True(changes.TryTake(Architect, OtherPlot, out _));
    }

    [Fact]
    public void Only_the_last_twenty_changes_are_kept()
    {
        var changes = new ArchitectChangeRegistry();
        for (var piece = 1; piece <= ArchitectChangeRegistry.MostChanges + 5; piece++)
            changes.Record(Architect, Change(ArchitectChangeKind.Placed, Plot, piece));

        var taken = new List<long>();
        while (changes.TryTake(Architect, Plot, out var change))
            taken.Add(change.PieceId);

        Assert.Equal(ArchitectChangeRegistry.MostChanges, taken.Count);
        Assert.Equal(ArchitectChangeRegistry.MostChanges + 5, taken[0]);
        Assert.Equal(6, taken[^1]);
    }

    [Fact]
    public void Moves_of_one_piece_in_a_row_undo_back_to_where_the_first_began()
    {
        var changes = new ArchitectChangeRegistry();
        changes.Record(Architect, Change(ArchitectChangeKind.Moved, Plot, 7) with { Position = new Vector3(1f, 0f, 0f) });
        changes.Record(Architect, Change(ArchitectChangeKind.Moved, Plot, 7) with { Position = new Vector3(2f, 0f, 0f) });
        changes.Record(Architect, Change(ArchitectChangeKind.Moved, Plot, 8) with { Position = new Vector3(3f, 0f, 0f) });

        Assert.True(changes.TryTake(Architect, Plot, out var other));
        Assert.Equal(8, other.PieceId);
        Assert.True(changes.TryTake(Architect, Plot, out var first));
        Assert.Equal(new Vector3(1f, 0f, 0f), first.Position);
        Assert.False(changes.TryTake(Architect, Plot, out _));
    }

    [Fact]
    public void A_piece_placed_again_is_named_by_its_new_id_and_a_removed_plot_leaves_nothing_to_undo()
    {
        var changes = new ArchitectChangeRegistry();
        changes.Record(Architect, Change(ArchitectChangeKind.Moved, Plot, 9));
        changes.Record(Other, Change(ArchitectChangeKind.Placed, OtherPlot, 10));

        changes.Renumber(Architect, 9, 90);
        Assert.True(changes.TryTake(Architect, Plot, out var moved));
        Assert.Equal(90, moved.PieceId);

        changes.ForgetPlot(OtherPlot);
        Assert.False(changes.TryTake(Other, OtherPlot, out _));
    }

    private static ArchitectChange Change(ArchitectChangeKind kind, long plot, long piece) =>
        new(kind, plot, piece, "furniture.table", Vector3.Zero, Vector3.Zero, []);
}
