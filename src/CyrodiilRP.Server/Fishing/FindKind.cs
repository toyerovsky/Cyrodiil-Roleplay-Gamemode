using ReadyM.Relay.Common.Oblivion;

namespace CyrodiilRP.Server.Fishing;

// A thing of the game's that comes up on the hook instead of a fish, and its share of the finds.
internal sealed record FindKind(FindKindName Kind, FormId Item, double Share);
