using CyrodiilRP.Server.Admin;

namespace CyrodiilRP.Tests.Admin;

// The game's data the server is built with (tools/esm/export_admin_data.py of the research).
public sealed class ShippedAdminDataTests
{
    private const uint LockpickForm = 0xA;
    private static readonly string[] PlaceKinds = ["gate", "city", "inn", "settlement"];

    [Fact]
    public void Every_place_has_its_own_id_and_a_known_kind()
    {
        var places = PlaceCatalog.Shipped.All;

        Assert.NotEmpty(places);
        Assert.Equal(places.Count, places.Select(p => p.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(places, place => Assert.Contains(place.Kind, PlaceKinds));
    }

    [Fact]
    public void Every_city_has_its_gate()
    {
        foreach (var city in new[] { "Anvil", "Bravil", "Bruma", "Cheydinhal", "Chorrol", "Imperial City", "Kvatch", "Leyawiin", "Skingrad" })
            Assert.Contains(PlaceCatalog.Shipped.All, place => place.Kind == "gate" && place.Name.StartsWith(city, StringComparison.Ordinal));
    }

    // One spot measured in the game in both units, a reference's position against its actor's: within the scale's
    // rounding.
    [Fact]
    public void The_game_units_turn_into_the_servers_centimetres_with_y_the_other_way()
    {
        var unreal = GameSpace.ToUnreal(-69744f, 103216f, 14413f);

        Assert.Equal(-99646.7f, unreal.X, 0.0002f * 99646.7f);
        Assert.Equal(-147469.9f, unreal.Y, 0.0002f * 147469.9f);
        Assert.Equal(20592.6f, unreal.Z, 0.0002f * 20592.6f);
        var back = GameSpace.ToGame(GameSpace.ToUnreal(1615f, 2421f, 6058f));
        Assert.Equal(1615f, back.X, 0.01f);
        Assert.Equal(2421f, back.Y, 0.01f);
        Assert.Equal(6058f, back.Z, 0.01f);
    }

    [Fact]
    public void An_item_is_found_by_its_editor_id_and_named_by_its_form()
    {
        var lockpick = ItemCatalog.Shipped.Find("lockpick").One;

        Assert.NotNull(lockpick);
        Assert.Equal(LockpickForm, lockpick.Form);
        Assert.Same(lockpick, ItemCatalog.Shipped.FindByForm(LockpickForm));
    }

    [Fact]
    public void The_search_puts_the_whole_name_first_and_stops_at_its_limit()
    {
        var found = ItemCatalog.Shipped.Search("sword", null, CatalogLookup.MostCandidates);

        Assert.Equal(CatalogLookup.MostCandidates, found.Count);
        Assert.Equal("Lockpick", ItemCatalog.Shipped.Search("lockpick", null, CatalogLookup.MostCandidates)[0].Name);
    }

    [Fact]
    public void A_type_narrows_the_search_and_alone_lists_that_type_by_name()
    {
        const int Most = 60;

        var weapons = ItemCatalog.Shipped.Search(null, "weap", Most);
        var ironArmour = ItemCatalog.Shipped.Search("iron", "ARMO", Most);

        Assert.Equal(Most, weapons.Count);
        Assert.All(weapons, item => Assert.Equal("WEAP", item.Type));
        Assert.Equal(weapons.Select(item => item.Name).Order(StringComparer.OrdinalIgnoreCase), weapons.Select(item => item.Name));
        Assert.NotEmpty(ironArmour);
        Assert.All(ironArmour, item => Assert.Equal("ARMO", item.Type));
        Assert.Empty(ItemCatalog.Shipped.Search(" ", "", Most));
    }

    [Fact]
    public void The_weathers_are_the_plain_ones_whatever_the_letter_case()
    {
        Assert.Equal(["clear", "cloudy", "overcast", "fog", "rain", "thunderstorm", "snow"], WeatherCatalog.Shipped.Names);
        Assert.True(WeatherCatalog.Shipped.TryFind("Rain", out _));
        Assert.False(WeatherCatalog.Shipped.TryFind("ash", out _));
    }
}
