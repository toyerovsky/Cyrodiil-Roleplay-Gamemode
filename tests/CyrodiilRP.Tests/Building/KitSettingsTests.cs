using CyrodiilRP.Server;
using CyrodiilRP.Server.Building;

namespace CyrodiilRP.Tests.Building;

public sealed class KitSettingsTests
{
    [Theory]
    [InlineData("SM_brumahouselower01_RoofSupports_A", "Roof supports A (01)")]
    [InlineData("SM_brumahouselower01_Wall_C", "Wall C (01)")]
    [InlineData("SM_anvillchouse_balk_a_01", "Balk A 01")]
    [InlineData("SM_brumahousemid04divider01", "brumahousemid04divider01")]
    public void A_part_is_named_by_its_words_after_the_house_and_the_house_s_number(string mesh, string name)
    {
        Assert.Equal(name, KitSettings.NameOf(mesh));
    }

    [Fact]
    public void A_part_is_keyed_by_its_mesh_and_stands_as_the_mesh_in_its_folder()
    {
        var kit = new KitSettings { Key = "bruma-lower", Name = "Bruma house, lower", Use = PieceUse.Residential, Meshes = ["bruma/Kit/SM_brumahouselower01_Wall_A"] };

        var piece = Assert.Single(kit.ToPieces());

        Assert.Equal("kit.brumahouselower01_wall_a", piece.Key);
        Assert.Equal("/Game/Art/Environment/Architecture/bruma/Kit/SM_brumahouselower01_Wall_A.SM_brumahouselower01_Wall_A", piece.Asset);
        Assert.Equal(PieceCategory.Kits, piece.Category);
        Assert.Equal("bruma-lower", piece.Kit);
        Assert.Equal(PieceUse.Residential, piece.Use);
    }

    [Fact]
    public void The_shipped_kits_make_a_catalogue_the_server_starts_with()
    {
        var settings = new BuildingSettings();

        settings.Check();

        var kitPieces = settings.ListPieces().Where(piece => piece.Category == PieceCategory.Kits).ToList();
        Assert.Equal(settings.Kits.Sum(kit => kit.Meshes.Count), kitPieces.Count);
        Assert.All(kitPieces, piece => Assert.StartsWith(KitSettings.KeyPrefix, piece.Key));
    }

    [Fact]
    public void A_kit_without_meshes_or_with_a_mesh_out_of_any_folder_stops_the_server()
    {
        Assert.Throws<InvalidOperationException>(() => new BuildingSettings { Kits = [new KitSettings { Key = "empty", Name = "Empty", Meshes = [] }] }.Check());
        Assert.Throws<InvalidOperationException>(() =>
            new BuildingSettings { Kits = [new KitSettings { Key = "loose", Name = "Loose", Meshes = ["SM_wall"] }] }.Check());
    }
}
