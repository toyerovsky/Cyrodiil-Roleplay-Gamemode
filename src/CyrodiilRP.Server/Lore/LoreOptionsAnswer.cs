namespace CyrodiilRP.Server.Lore;

// op options: what the field may pick. Message: why there is nothing, null when there is something.
internal sealed record LoreOptionsAnswer(bool Ok, string Field, IReadOnlyList<LoreOptionJson> Options, string? Message);
