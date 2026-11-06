namespace CyrodiilRP.Server.Sanctuary;

// What the guardian keeps of one player online. Times are on the guardian's clock, in seconds.
internal sealed class PlayerWatch(Guid account)
{
    public Guid Account { get; } = account;

    public Ward Ward { get; } = new();

    public Aggression Aggression { get; } = new();

    public IncidentTrack Incidents { get; } = new();

    // Null until the player was first seen placed in an area.
    public bool? Inside { get; set; }

    public double LastAttack { get; private set; } = double.NegativeInfinity;

    public double LastCast { get; private set; } = double.NegativeInfinity;

    public double ShieldedAt { get; set; } = double.NegativeInfinity;

    // When the staff last wrote the health (/ahp), which is neither a hit nor put back.
    public double StaffWriteAt { get; set; } = double.NegativeInfinity;

    public bool DeathCounted { get; set; }

    public HudState Shown { get; set; } = HudState.Hidden;

    // A swing seen in this tick counts for a hit in this tick.
    public void NoteFighting(bool attacking, bool casting, double now)
    {
        if (attacking)
            LastAttack = now;

        if (casting)
            LastCast = now;
    }

    // The player's ward starts over: the next look takes the health as it is.
    public void Reset()
    {
        Ward.Forget();
        Inside = null;
        DeathCounted = false;
    }
}
