using CyrodiilRP.Server.Lore;

namespace CyrodiilRP.Tests.Lore;

public sealed class GameRacesTests
{
    // A stranger sees the race's English lore name, the same for both sexes, never translated.
    [Theory]
    [InlineData(0x00000907u, "Imperial")]
    [InlineData(0x000224FDu, "Nord")]
    [InlineData(0x000224FCu, "Breton")]
    [InlineData(0x00000D43u, "Redguard")]
    [InlineData(0x000191C1u, "Dunmer")]
    [InlineData(0x000223C8u, "Bosmer")]
    [InlineData(0x00019204u, "Altmer")]
    [InlineData(0x00023FE9u, "Argonian")]
    [InlineData(0x000223C7u, "Khajiit")]
    [InlineData(0x000191C0u, "Orc")]
    [InlineData(0x00123456u, GameRaces.StrangerLabel)]
    public void A_stranger_reads_the_races_lore_name(uint formId, string label)
    {
        Assert.Equal(label, GameRaces.Label(GameRaces.FromFormId(formId)));
    }

    [Theory]
    [InlineData("/Game/Forms/Races/DarkElf.DarkElf", nameof(GameRace.DarkElf))]
    [InlineData("/Game/Races/WoodElf_Race", nameof(GameRace.WoodElf))]
    [InlineData("/Game/Races/HighElf", nameof(GameRace.HighElf))]
    [InlineData("/Game/Races/Orc", nameof(GameRace.Orc))]
    [InlineData("/Game/Races/Dremora", nameof(GameRace.Unknown))]
    public void A_race_path_names_the_race_when_the_form_id_does_not(string path, string race)
    {
        Assert.Equal(Enum.Parse<GameRace>(race), GameRaces.FromPath(path));
    }

    [Fact]
    public void Every_race_of_the_game_is_an_entry_of_the_lore_list()
    {
        foreach (var race in Enum.GetValues<GameRace>())
        {
            var id = GameRaces.FindCatalogId(race);
            if (race == GameRace.Unknown)
                Assert.Null(id);
            else
                Assert.True(LoreCatalog.Shipped.TryGet(id, out var entry) && entry.Kind == LoreKind.Race, $"{race} has no race entry.");
        }
    }
}
