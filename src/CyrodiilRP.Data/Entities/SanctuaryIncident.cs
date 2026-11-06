namespace CyrodiilRP.Data;

// Append only: hits on one victim in one area of the Sanctuary close together, for the staff.
public sealed class SanctuaryIncident
{
    public long Id { get; set; }

    // When the first hit was seen.
    public DateTimeOffset At { get; set; }

    // The game's id of the area and the name players know it by.
    public required string Area { get; set; }

    public required string AreaName { get; set; }

    public SanctuaryVictim VictimKind { get; set; }

    // The ReadyM account of a player, null for an NPC.
    public Guid? VictimAccount { get; set; }

    // A player's real name as it was then, or "NPC <ref id>".
    public required string VictimName { get; set; }

    public float Damage { get; set; }

    public bool Died { get; set; }

    public SanctuaryVerdict Verdict { get; set; }

    // The players who lost the Sanctuary's protection for it, their names as they were then and their accounts in the
    // same order.
    public string[] Aggressors { get; set; } = [];

    public Guid[] AggressorAccounts { get; set; } = [];
}
