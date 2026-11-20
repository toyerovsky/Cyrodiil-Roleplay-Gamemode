using CyrodiilRP.Data.Gambling;
using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Server.Gambling;

internal sealed class GamblingSession(GamblingTableRecord table, PlayerAsk invite, GamblingMatch match, long expiresAt)
{
    public Guid OperationId { get; } = Guid.NewGuid();
    public GamblingTableRecord Table { get; } = table;
    public PlayerAsk Invite { get; } = invite;
    public GamblingMatch Match { get; } = match;
    public GamblingStage Stage { get; set; } = GamblingStage.Invited;
    public TaskCompletionSource<bool> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool CancellationRequested { get; set; }
    public long Id { get; set; }
    public int Version { get; set; }
    public long ExpiresAt { get; set; } = expiresAt;
    public DateTimeOffset StartedAt { get; set; }
    public bool FirstGone { get; private set; }
    public bool SecondGone { get; private set; }
    public GamblingStatus EndStatus { get; set; } = GamblingStatus.Open;
    public string EndActor { get; set; } = Infrastructure.Audit.ServerRef;
    public long FirstCharacterId { get; } = invite.Asker.Character!.Id;
    public long SecondCharacterId { get; } = invite.Asked.Character!.Id;
    public string FirstName { get; } = invite.Asker.Name;
    public string SecondName { get; } = invite.Asked.Name;
    public OnlinePlayer Player(int side) => side == GamblingMatch.First ? Invite.Asker : Invite.Asked;
    public int Side(OnlinePlayer player) => player == Invite.Asker ? GamblingMatch.First : player == Invite.Asked ? GamblingMatch.Second
        : throw new ArgumentException("The player is not in this match.", nameof(player));
    public bool Contains(OnlinePlayer player) => player == Invite.Asker || player == Invite.Asked;

    public void MarkGone(bool first, bool second)
    {
        FirstGone |= first;
        SecondGone |= second;
    }
}
