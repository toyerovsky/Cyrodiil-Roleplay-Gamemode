namespace CyrodiilRP.Data;

// A cap the staff set on one of a character's attributes or skills, in place of what the specialization gives it.
// Stat: the SDK's stat by name.
public sealed class CharacterCapOverride
{
    public long CharacterId { get; set; }

    public string Stat { get; set; } = "";

    public int Cap { get; set; }
}
