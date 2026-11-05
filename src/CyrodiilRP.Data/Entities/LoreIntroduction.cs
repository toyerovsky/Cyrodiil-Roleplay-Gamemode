namespace CyrodiilRP.Data;

// The knower's character knows the known's name. An accepted introduction writes both directions.
public sealed class LoreIntroduction
{
    public long KnowerCharacterId { get; set; }

    public long KnownCharacterId { get; set; }

    public DateTimeOffset At { get; set; }
}
