using CyrodiilRP.Data;
using CyrodiilRP.Server.Localization;

namespace CyrodiilRP.Server.Guide;

// A guide as the catalogue holds it, its numbers already in its phrases, before it is put in a reader's language.
internal sealed record GuideCard(GuideTopic Topic, Phrase Title, Phrase Purpose, IReadOnlyList<GuideStep> Steps, IReadOnlyList<GuideFact> Facts);
