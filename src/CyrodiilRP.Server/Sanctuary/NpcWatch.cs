namespace CyrodiilRP.Server.Sanctuary;

// What the guardian keeps of one NPC inside the Sanctuary.
internal sealed class NpcWatch(uint refId)
{
    public uint RefId { get; } = refId;

    public Ward Ward { get; } = new();

    public IncidentTrack Incidents { get; } = new();

    public bool DeathCounted { get; set; }

    // How the incident log and the staff name it.
    public string Name => $"NPC {RefId:X8}";
}
