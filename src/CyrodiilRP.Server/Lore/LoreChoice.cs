using CyrodiilRP.Server.Localization;

namespace CyrodiilRP.Server.Lore;

// Id: what the card stores. Word: how it reads, in lower case.
internal sealed record LoreChoice(string Id, Phrase Word);
