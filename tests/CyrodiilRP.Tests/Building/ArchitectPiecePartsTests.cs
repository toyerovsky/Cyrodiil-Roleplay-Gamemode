using System.Text;
using System.Text.Json;
using CyrodiilRP.Server;
using CyrodiilRP.Server.Building;

namespace CyrodiilRP.Tests.Building;

public sealed class ArchitectPiecePartsTests
{
    // The most bytes a string of an RPC carries.
    private const int MessageStringBytes = ushort.MaxValue;

    [Fact]
    public void The_shipped_catalogue_goes_in_parts_a_message_carries_and_together_they_hold_every_piece_in_order()
    {
        var views = new BuildingSettings().ListPieces()
            .Select(piece => new ArchitectPieceView(piece.Key, piece.Name, piece.Category, piece.Use, piece.Kit))
            .ToList();

        var parts = ArchitectPieceParts.Write(views, ArchitectPieceParts.MostBytes);

        Assert.True(parts.Count > 1);
        Assert.All(parts, part => Assert.InRange(Encoding.UTF8.GetByteCount(part), 1, ArchitectPieceParts.MostBytes));
        Assert.True(ArchitectPieceParts.MostBytes < MessageStringBytes);
        var read = parts.SelectMany(part => JsonSerializer.Deserialize<List<JsonElement>>(part)!).Select(piece => piece.GetProperty("key").GetString());
        Assert.Equal(views.Select(view => view.Key), read);
    }

    [Fact]
    public void A_piece_bigger_than_a_part_goes_alone_and_no_pieces_go_as_an_empty_list()
    {
        var views = new[] { new ArchitectPieceView("a", "A", PieceCategory.Kits, PieceUse.Any, "kit"), new ArchitectPieceView("b", "B", PieceCategory.Kits, PieceUse.Any, "kit") };

        Assert.Equal(2, ArchitectPieceParts.Write(views, 10).Count);
        Assert.Equal(["[]"], ArchitectPieceParts.Write([], ArchitectPieceParts.MostBytes));
    }
}
