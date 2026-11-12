namespace CyrodiilRP.Data;

// The one horse a character owns. A purchase writes over the horse before it, so a character without a row has none.
public sealed class CharacterHorse
{
    public long CharacterId { get; set; }

    public HorseKind Horse { get; set; }

    // The gold it cost.
    public int Price { get; set; }

    public DateTimeOffset BoughtAt { get; set; }
}
