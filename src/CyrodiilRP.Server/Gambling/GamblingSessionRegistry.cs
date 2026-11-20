using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Server.Gambling;

internal sealed class GamblingSessionRegistry
{
    private readonly List<GamblingSession> _sessions = [];
    public IReadOnlyList<GamblingSession> Sessions => _sessions;
    public GamblingSession? Find(OnlinePlayer player) => _sessions.FirstOrDefault(session => session.FirstCharacterId == player.Character?.Id
        || session.SecondCharacterId == player.Character?.Id);
    public GamblingSession? Find(long id) => _sessions.FirstOrDefault(session => session.Id == id && id > 0);
    public int Count(long tableId) => _sessions.Count(session => session.Table.Id == tableId);
    public void Add(GamblingSession session)
    {
        if (Find(session.Invite.Asker) is not null || Find(session.Invite.Asked) is not null)
            throw new InvalidOperationException("A character already has a game or invitation.");

        _sessions.Add(session);
    }
    public void Remove(GamblingSession session) => _sessions.Remove(session);
}
