namespace CyrodiilRP.Data;

// Who the Sanctuary blamed for a drop in health: stored by name.
public enum SanctuaryVerdict
{
    // Nobody near was fighting: a fall, an NPC.
    None,

    // One player was fighting next to the victim.
    Suspect,

    // More than one was, and all of them count.
    Several,
}
