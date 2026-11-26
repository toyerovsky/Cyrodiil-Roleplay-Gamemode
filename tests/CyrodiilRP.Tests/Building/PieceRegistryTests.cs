using System.Numerics;
using CyrodiilRP.Data.Building;
using CyrodiilRP.Server;
using CyrodiilRP.Server.Building;

namespace CyrodiilRP.Tests.Building;

public sealed class PieceRegistryTests
{
    private static readonly Vector3 Min = new(-40f, -60f, 0f);
    private static readonly Vector3 Max = new(40f, 60f, 90f);

    [Fact]
    public void The_drawer_lists_the_catalogue_in_the_order_of_the_config()
    {
        var settings = new BuildingSettings();

        var registry = new PieceRegistry(settings.Pieces);

        Assert.Equal(settings.Pieces.Select(p => p.Key), registry.All.Select(p => p.Key));
        Assert.All(registry.All, piece => Assert.Null(piece.Bounds));
    }

    [Fact]
    public void The_first_measurement_of_a_piece_stands()
    {
        var registry = new PieceRegistry(new BuildingSettings().Pieces);

        Assert.True(registry.Measure("furniture.table", Min, Max));
        Assert.True(registry.Measure("furniture.table", Min * 2f, Max * 2f));

        Assert.True(registry.TryFind("furniture.table", out var table));
        Assert.Equal(new PieceBounds(Min, Max), table.Bounds);
    }

    [Fact]
    public void A_box_for_no_piece_of_the_catalogue_or_no_box_at_all_is_refused()
    {
        var registry = new PieceRegistry(new BuildingSettings().Pieces);

        Assert.False(registry.Measure("furniture.throne", Min, Max));
        Assert.False(registry.Measure("furniture.table", Max, Min));
        Assert.False(registry.Measure("furniture.table", Min with { X = float.NaN }, Max));
        Assert.False(registry.Measure("furniture.table", Min, Max with { Z = 1_000_000f }));

        Assert.True(registry.TryFind("furniture.table", out var table));
        Assert.Null(table.Bounds);
    }

    [Fact]
    public void The_shipped_catalogue_passes_the_check_of_the_config()
    {
        var settings = new BuildingSettings();

        settings.Check();

        Assert.All(settings.Pieces, piece => Assert.StartsWith("/Game/Forms/worldobjects/", piece.Asset));
        Assert.Contains(settings.Pieces, piece => piece.Object == PieceObject.Furniture);
        Assert.Contains(settings.Pieces, piece => piece.Object == PieceObject.Container);
        Assert.All(settings.Pieces.Where(piece => piece.Object == PieceObject.Door), door =>
        {
            Assert.Equal(PieceCategory.Doors, door.Category);
            Assert.StartsWith("/Game/Forms/worldobjects/door/", door.Asset);
            Assert.NotEqual(0u, door.ReadForm());
        });
    }

    [Fact]
    public void The_game_s_furniture_and_containers_come_with_their_form_and_a_mesh_with_none()
    {
        var registry = new PieceRegistry(new BuildingSettings().Pieces);

        Assert.True(registry.TryFind("furniture.chair", out var chair));
        Assert.Equal(PieceObject.Furniture, chair.Object);
        Assert.Equal(0x0002F18Au, chair.FormId);
        Assert.True(registry.TryFind("furniture.chest", out var chest));
        Assert.Equal(PieceObject.Container, chest.Object);
        Assert.Equal(0x000086C1u, chest.FormId);
        Assert.True(registry.TryFind("furniture.table", out var table));
        Assert.Equal(PieceObject.Mesh, table.Object);
        Assert.Equal(0u, table.FormId);
    }

    [Fact]
    public void Furniture_without_a_form_or_a_mesh_with_one_stops_the_mod()
    {
        var formless = new BuildingSettings();
        formless.Pieces.Add(new PieceSettings { Key = "x.chair", Name = "Chair", Asset = "/Game/Chair.Chair", Object = PieceObject.Furniture });
        var meshWithForm = new BuildingSettings();
        meshWithForm.Pieces.Add(new PieceSettings { Key = "x.rock", Name = "Rock", Asset = "/Game/Rock.Rock", Form = "0x00012345" });
        var notHex = new BuildingSettings();
        notHex.Pieces.Add(new PieceSettings { Key = "x.box", Name = "Box", Asset = "/Game/Box.Box", Object = PieceObject.Container, Form = "chest" });

        Assert.Throws<InvalidOperationException>(formless.Check);
        Assert.Throws<InvalidOperationException>(meshWithForm.Check);
        Assert.Throws<InvalidOperationException>(notHex.Check);
    }

    [Fact]
    public void A_key_listed_twice_or_longer_than_its_column_stops_the_mod()
    {
        var twice = new BuildingSettings();
        twice.Pieces.Add(new PieceSettings { Key = twice.Pieces[0].Key, Name = "Again", Asset = "/Game/Again.Again" });
        var tooLong = new BuildingSettings();
        tooLong.Pieces.Add(new PieceSettings { Key = new string('k', PlotBook.MostPieceLength + 1), Name = "Long", Asset = "/Game/Long.Long" });

        Assert.Throws<InvalidOperationException>(twice.Check);
        Assert.Throws<InvalidOperationException>(tooLong.Check);
    }
}
