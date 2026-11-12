namespace CyrodiilRP.Data;

// A notice pinned to a board. The board shows it unsigned: CharacterId is for the staff and the limits only.
public sealed class Notice
{
    public long Id { get; set; }

    public long BoardId { get; set; }

    public long CharacterId { get; set; }

    public string Text { get; set; } = string.Empty;

    public DateTimeOffset PostedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }
}
