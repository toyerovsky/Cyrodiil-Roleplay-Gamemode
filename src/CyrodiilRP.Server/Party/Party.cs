using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Server.Party;

// The members in the order they joined. The one who leads is one of them.
internal sealed class Party(OnlinePlayer leader)
{
    private readonly List<OnlinePlayer> _members = [leader];

    public OnlinePlayer Leader { get; set; } = leader;

    public IReadOnlyList<OnlinePlayer> Members => _members;

    // The leader first, then the order they joined.
    public IEnumerable<OnlinePlayer> Listed => _members.OrderBy(member => member == Leader ? 0 : 1);

    public bool Contains(OnlinePlayer player) => _members.Contains(player);

    public void Add(OnlinePlayer player) => _members.Add(player);

    // One who led hands the lead to the next who joined.
    public void Remove(OnlinePlayer player)
    {
        _members.Remove(player);
        if (Leader == player && _members.Count > 0)
            Leader = _members[0];
    }
}
