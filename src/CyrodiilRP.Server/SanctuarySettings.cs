namespace CyrodiilRP.Server;

internal sealed class SanctuarySettings
{
    // Whether it runs as the server starts. The staff turn it on and off at runtime with /asanctuary.
    public bool Enabled { get; set; } = true;

    // Area ids added to the Imperial City, or taken out of it. The Arena is never part of the Sanctuary.
    public List<string> ExtraAreas { get; set; } = [];

    public List<string> ExcludedAreas { get; set; } = [];

    // For protected players. True: every drop in health is put back, from a fall, an NPC or a player. False: only drops
    // a player is suspected of, which leaves room for what hurts on purpose (hunger, poison). NPCs always get every drop
    // back.
    public bool HealAllDamage { get; set; } = true;

    // How a hit is pinned on a player: a swing within MeleeRange, or a swing or cast within RangedRange, no longer than
    // AttackWindowSeconds before the health dropped. Distances in centimetres.
    public float MeleeRange { get; set; } = 400f;

    public float RangedRange { get; set; } = 4000f;

    public float AttackWindowSeconds { get; set; } = 1.5f;

    // A player who attacks anyone inside the Sanctuary loses its protection for this long after the last attack, or until
    // they die.
    public float AggressorSeconds { get; set; } = 300f;

    // Added when a player becomes an aggressor, so the guards go after them. 0 adds none.
    public float Bounty { get; set; } = 40f;

    // Hits on the same victim this close together are one incident in the log.
    public float IncidentSeconds { get; set; } = 3f;
}
