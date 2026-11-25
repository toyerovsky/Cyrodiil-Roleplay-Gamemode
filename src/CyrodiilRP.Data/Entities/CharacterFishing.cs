namespace CyrodiilRP.Data;

// A character's fishing: the skill's level and the xp into it, how many fish it landed, and the bait it fishes with (a
// BaitKind's name, kept by the server). A character without a row has never fished.
public sealed class CharacterFishing
{
    public long CharacterId { get; set; }

    public int Level { get; set; }

    public int Xp { get; set; }

    public int Catches { get; set; }

    public string Bait { get; set; } = "";

    public DateTimeOffset UpdatedAt { get; set; }
}
