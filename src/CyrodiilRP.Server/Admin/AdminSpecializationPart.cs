namespace CyrodiilRP.Server.Admin;

// What a card shows of the specialization, as AdminCardView names it.
internal sealed record AdminSpecializationPart(string? Name, string? Id, IReadOnlyList<AdminStatView>? Stats);
