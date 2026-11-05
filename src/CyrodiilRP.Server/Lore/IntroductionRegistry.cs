namespace CyrodiilRP.Server.Lore;

// Who knows whom, among the characters in play and those they met. Each direction on its own: an introduction adds both,
// a forgetting takes one.
internal sealed class IntroductionRegistry
{
    private readonly HashSet<Introduction> _known = [];

    public bool Knows(long knower, long known) => _known.Contains(new Introduction(knower, known));

    public void Add(Introduction introduction) => _known.Add(introduction);

    public bool Remove(Introduction introduction) => _known.Remove(introduction);

    public IEnumerable<long> ListKnowers(long known) => _known.Where(i => i.Known == known).Select(i => i.Knower);

    // A character that left: what it knows and who knows it come back with its next load.
    public void Drop(long characterId) => _known.RemoveWhere(i => i.Knower == characterId || i.Known == characterId);
}
