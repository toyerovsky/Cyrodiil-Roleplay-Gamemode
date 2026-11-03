namespace CyrodiilRP.Data;

public sealed class Account
{
    public long Id { get; set; }

    // The same on every server and across reconnects, unlike the platform's player id.
    public Guid ReadyMId { get; set; }

    public string Locale { get; set; } = "en";

    // For the staff: the platform lets anyone take any nickname.
    public string LastNickname { get; set; } = "";

    public string[] Roles { get; set; } = [];

    public DateTimeOffset? BannedUntil { get; set; }

    public string? BanReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }

    public List<Character> Characters { get; set; } = [];
}
