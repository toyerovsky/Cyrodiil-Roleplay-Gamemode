namespace CyrodiilRP.Data;

// A character is in one faction at most, so the character is the key.
public sealed class FactionMember
{
    public long CharacterId { get; set; }

    public long FactionId { get; set; }

    public int RankNumber { get; set; }

    public DateTimeOffset JoinedAt { get; set; }
}
