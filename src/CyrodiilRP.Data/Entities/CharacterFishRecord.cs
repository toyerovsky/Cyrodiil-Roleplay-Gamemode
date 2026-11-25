namespace CyrodiilRP.Data;

// A character's best fish of a species (a FishSpecies' name, kept by the server): its length and weight, how many of the
// species it landed, and when the best one came out.
public sealed class CharacterFishRecord
{
    public long CharacterId { get; set; }

    public string Species { get; set; } = "";

    public int BestLengthCm { get; set; }

    public int BestGrams { get; set; }

    public int Count { get; set; }

    public DateTimeOffset BestAt { get; set; }
}
