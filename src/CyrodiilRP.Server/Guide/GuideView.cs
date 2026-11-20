using CyrodiilRP.Data;

namespace CyrodiilRP.Server.Guide;

// A guide's card as the page shows it, in the reader's language.
internal sealed record GuideView(
    GuideTopic Topic,
    GuideMode Mode,
    string Title,
    string Purpose,
    IReadOnlyList<GuideStepView> Steps,
    IReadOnlyList<GuideFactView> Facts);
