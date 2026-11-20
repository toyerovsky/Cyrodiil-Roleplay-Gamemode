using CyrodiilRP.Server.Localization;

namespace CyrodiilRP.Server.Guide;

// Keys: the plates of the keys the step is done with, as the keyboard reads them ("G", "Shift+End"), none for most.
internal sealed record GuideStep(Phrase Text, IReadOnlyList<string> Keys);
