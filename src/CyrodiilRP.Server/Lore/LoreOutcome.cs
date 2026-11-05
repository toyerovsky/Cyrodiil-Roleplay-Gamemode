using CyrodiilRP.Server.Localization;

namespace CyrodiilRP.Server.Lore;

// What an introduction, an answer or a forgetting came to, for the one who asked.
internal sealed record LoreOutcome(bool Ok, Phrase Message);
