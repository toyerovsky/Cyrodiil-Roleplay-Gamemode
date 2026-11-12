using CyrodiilRP.Data;

namespace CyrodiilRP.Server.Stable;

// Owned: the character's own horse, which the window does not sell again.
internal sealed record StableHorseView(HorseKind Id, int Price, bool Owned);
