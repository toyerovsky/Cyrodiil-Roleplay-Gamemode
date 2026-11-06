namespace CyrodiilRP.Server.Faction;

// Every faction, in memory from the start of the server. A name being founded is held until its write is done, so two
// founders never get the same one, and one founder never two factions.
internal sealed class FactionRegistry
{
    private readonly Dictionary<long, FactionRecord> _byId = [];
    private readonly Dictionary<long, FactionRecord> _byMember = [];
    private readonly Dictionary<string, long> _founding = new(StringComparer.Ordinal);

    public IEnumerable<FactionRecord> All => _byId.Values.OrderBy(faction => faction.Name, StringComparer.OrdinalIgnoreCase);

    public FactionRecord? Find(long id) => _byId.GetValueOrDefault(id);

    public FactionRecord? FindOf(long characterId) => _byMember.GetValueOrDefault(characterId);

    public bool IsNameTaken(string name)
    {
        var key = Data.Faction.MakeNameKey(name);
        return _founding.ContainsKey(key) || _byId.Values.Any(faction => Data.Faction.MakeNameKey(faction.Name) == key);
    }

    public bool IsFounding(long characterId) => _founding.ContainsValue(characterId);

    public void Reserve(string name, long founderCharacterId) => _founding.Add(Data.Faction.MakeNameKey(name), founderCharacterId);

    public void Release(string name) => _founding.Remove(Data.Faction.MakeNameKey(name));

    public void Add(FactionRecord faction)
    {
        _byId.Add(faction.Id, faction);
        foreach (var member in faction.Members)
            _byMember.Add(member.CharacterId, faction);
    }

    public void Remove(FactionRecord faction)
    {
        _byId.Remove(faction.Id);
        foreach (var member in faction.Members)
            _byMember.Remove(member.CharacterId);
    }

    public void AddMember(FactionRecord faction, FactionMemberRecord member)
    {
        _byMember.Add(member.CharacterId, faction);
        faction.PutMember(member);
    }

    public void RemoveMember(FactionRecord faction, long characterId)
    {
        _byMember.Remove(characterId);
        faction.RemoveMember(characterId);
    }
}
