namespace CyrodiilRP.Data;

// The specialization a character chose. A change writes over the one before, so a character without a row has none yet.
public sealed class CharacterSpecialization
{
    public long CharacterId { get; set; }

    public SpecializationKind Specialization { get; set; }

    // The gold the last choice cost: 0 for the first.
    public int Price { get; set; }

    public DateTimeOffset ChosenAt { get; set; }

    // How many times the character changed it after the first choice.
    public int Changes { get; set; }
}
