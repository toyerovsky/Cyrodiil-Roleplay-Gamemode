namespace CyrodiilRP.Data;

// A rank of one faction. Number: its name in commands and rows, kept for good. Position: its place from the top, 0 the
// leader's.
public sealed class FactionRank
{
    public const int LeaderNumber = 1;

    public const int EntryNumber = 2;

    public long FactionId { get; set; }

    public int Number { get; set; }

    public required string Name { get; set; }

    public int Position { get; set; }

    public FactionRankKind Kind { get; set; }

    public List<FactionPermission> Permissions { get; set; } = [];
}
