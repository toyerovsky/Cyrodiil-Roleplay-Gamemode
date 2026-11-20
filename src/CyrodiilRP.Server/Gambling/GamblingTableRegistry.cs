namespace CyrodiilRP.Server.Gambling;

internal sealed class GamblingTableRegistry
{
    private readonly Dictionary<long, GamblingTableRecord> _tables = [];
    public IReadOnlyCollection<GamblingTableRecord> Tables => _tables.Values;
    public void Add(GamblingTableRecord table) => _tables[table.Id] = table;
    public bool Contains(long id) => _tables.ContainsKey(id);
    public void Remove(long id) => _tables.Remove(id);
}
