namespace CyrodiilRP.Server.Lore;

// The races by the form ids of Oblivion.esm (UESP, Oblivion:Races), the name a stranger sees and the lore list's entry.
// A stranger's name is the race's English lore name, the same for both sexes, and never translated.
internal static class GameRaces
{
    public const string StrangerLabel = "Stranger";

    public static GameRace FromFormId(uint formId) => formId switch
    {
        0x00000907 => GameRace.Imperial,
        0x000224FD => GameRace.Nord,
        0x000224FC => GameRace.Breton,
        0x00000D43 => GameRace.Redguard,
        0x000191C1 => GameRace.DarkElf,
        0x000223C8 => GameRace.WoodElf,
        0x00019204 => GameRace.HighElf,
        0x00023FE9 => GameRace.Argonian,
        0x000223C7 => GameRace.Khajiit,
        0x000191C0 => GameRace.Orc,
        _ => GameRace.Unknown,
    };

    // The race's asset path, as the game reports it ("/Game/.../DarkElf"), when its form id is not one of the above.
    public static GameRace FromPath(string path)
    {
        var flat = new string(path.Where(char.IsLetter).ToArray()).ToLowerInvariant();
        foreach (var (word, race) in PathWords)
        {
            if (flat.Contains(word, StringComparison.Ordinal))
                return race;
        }

        return GameRace.Unknown;
    }

    public static string Label(GameRace race) => race switch
    {
        GameRace.Imperial => "Imperial",
        GameRace.Nord => "Nord",
        GameRace.Breton => "Breton",
        GameRace.Redguard => "Redguard",
        GameRace.DarkElf => "Dunmer",
        GameRace.WoodElf => "Bosmer",
        GameRace.HighElf => "Altmer",
        GameRace.Argonian => "Argonian",
        GameRace.Khajiit => "Khajiit",
        GameRace.Orc => "Orc",
        GameRace.Unknown => StrangerLabel,
    };

    public static string? FindCatalogId(GameRace race) => race switch
    {
        GameRace.Imperial => "race.imperial",
        GameRace.Nord => "race.nord",
        GameRace.Breton => "race.breton",
        GameRace.Redguard => "race.redguard",
        GameRace.DarkElf => "race.dunmer",
        GameRace.WoodElf => "race.bosmer",
        GameRace.HighElf => "race.altmer",
        GameRace.Argonian => "race.argonian",
        GameRace.Khajiit => "race.khajiit",
        GameRace.Orc => "race.orsimer",
        GameRace.Unknown => null,
    };

    // The game's own names and the lore names. Longer words first, so "darkelf" is never read as an elf of another kind.
    private static readonly IReadOnlyList<KeyValuePair<string, GameRace>> PathWords =
    [
        new("argonian", GameRace.Argonian),
        new("breton", GameRace.Breton),
        new("darkelf", GameRace.DarkElf),
        new("dunmer", GameRace.DarkElf),
        new("highelf", GameRace.HighElf),
        new("altmer", GameRace.HighElf),
        new("woodelf", GameRace.WoodElf),
        new("bosmer", GameRace.WoodElf),
        new("imperial", GameRace.Imperial),
        new("khajiit", GameRace.Khajiit),
        new("redguard", GameRace.Redguard),
        new("orsimer", GameRace.Orc),
        new("nord", GameRace.Nord),
        new("orc", GameRace.Orc),
    ];
}
