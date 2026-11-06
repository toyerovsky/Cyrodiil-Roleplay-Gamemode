using CyrodiilRP.Data;

namespace CyrodiilRP.Server.Faction;

internal sealed record StaffFactionRankView(int Number, string Name, FactionRankKind Kind, IReadOnlyList<FactionPermission> Permissions, int Members);
