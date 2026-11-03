using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Server.Commands;

internal sealed record AccountTarget(Guid ReadyMId, OnlinePlayer? Online);
