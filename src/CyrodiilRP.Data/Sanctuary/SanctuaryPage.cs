namespace CyrodiilRP.Data.Sanctuary;

// More: there are older incidents below the last one.
public sealed record SanctuaryPage(IReadOnlyList<SanctuaryIncident> Incidents, bool More);
