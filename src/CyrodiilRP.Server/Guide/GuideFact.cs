using CyrodiilRP.Server.Localization;

namespace CyrodiilRP.Server.Guide;

// A number worth knowing, from the rules or the config, and what it is.
internal sealed record GuideFact(Phrase Label, Phrase Value);
