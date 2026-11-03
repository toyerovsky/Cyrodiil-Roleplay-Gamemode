namespace CyrodiilRP.Data;

public sealed class StaffRole
{
    public const string Owner = "owner";
    public const string Admin = "admin";
    public const string Moderator = "moderator";

    public required string Name { get; set; }

    public required string Label { get; set; }

    // Names of restricted commands.
    public string[] Permissions { get; set; } = [];

    // Every permission there is. Only the owners from the server config hold such a role.
    public bool GrantsAll { get; set; }
}
