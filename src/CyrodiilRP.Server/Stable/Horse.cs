using CyrodiilRP.Data;

namespace CyrodiilRP.Server.Stable;

// The horse a player's character owns. CalledRefId: the one standing in the world for them now, 0 before it is called.
internal sealed record Horse(long CharacterId, HorseKind Kind, uint CalledRefId);
