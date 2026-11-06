namespace Roleplay.Jobs.Client;

public enum ChopEndReason
{
    /// <summary>A tree this player hit came down; the message lists what they got.</summary>
    Felled,

    /// <summary>The server would not count the hit, or the tree would not come down; the message says why.</summary>
    Refused,

    /// <summary>The hit glanced off a dull axe and did nothing.</summary>
    Glanced,
}
