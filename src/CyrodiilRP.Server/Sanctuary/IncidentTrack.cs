namespace CyrodiilRP.Server.Sanctuary;

// The incident one victim has open. A hit in another area starts a new one, and one that goes quiet is done.
internal sealed class IncidentTrack
{
    public OpenIncident? Open { get; private set; }

    // The incident a hit in another area closed, to be logged, or null.
    public OpenIncident? Record(string area, double now, float lost, bool died, IEnumerable<IncidentPerson> suspects, Func<OpenIncident> start)
    {
        OpenIncident? closed = null;
        if (Open is { } previous && previous.Area != area)
            closed = Close();

        var open = Open ??= start();
        open.Add(now, lost, died, suspects);
        return closed;
    }

    // The open incident once nothing was added to it for this long, or null.
    public OpenIncident? CloseIfQuiet(double now, double quietSeconds) =>
        Open is { } open && now - open.Last >= quietSeconds ? Close() : null;

    public OpenIncident? Close()
    {
        var open = Open;
        Open = null;
        return open;
    }
}
