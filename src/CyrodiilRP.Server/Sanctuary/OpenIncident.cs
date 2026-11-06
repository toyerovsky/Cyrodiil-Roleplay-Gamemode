using CyrodiilRP.Data;

namespace CyrodiilRP.Server.Sanctuary;

// Hits on one victim in one area close together, gathered until they go quiet and are logged as one.
internal sealed class OpenIncident(SanctuaryVictim kind, IncidentPerson? player, string victimName, string area, double start)
{
    private readonly List<IncidentPerson> _suspects = [];

    public SanctuaryVictim Kind { get; } = kind;

    // The victim when it is a player, null for an NPC.
    public IncidentPerson? Player { get; } = player;

    public string VictimName { get; } = victimName;

    public string Area { get; } = area;

    public DateTimeOffset At { get; } = DateTimeOffset.UtcNow;

    // On the guardian's clock.
    public double Last { get; private set; } = start;

    public float Damage { get; private set; }

    public bool Died { get; private set; }

    public IReadOnlyList<IncidentPerson> Suspects => _suspects;

    public void Add(double now, float lost, bool died, IEnumerable<IncidentPerson> suspects)
    {
        Last = now;
        Damage += lost;
        Died |= died;
        foreach (var suspect in suspects)
        {
            if (!_suspects.Any(known => known.Account == suspect.Account))
                _suspects.Add(suspect);
        }
    }

    public SanctuaryVerdict Verdict => _suspects.Count switch
    {
        0 => SanctuaryVerdict.None,
        1 => SanctuaryVerdict.Suspect,
        _ => SanctuaryVerdict.Several,
    };
}
