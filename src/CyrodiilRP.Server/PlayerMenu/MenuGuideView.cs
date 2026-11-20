using CyrodiilRP.Data;

namespace CyrodiilRP.Server.PlayerMenu;

// A guide the Help tab lists, to be read again.
internal sealed record MenuGuideView(GuideTopic Topic, string Title, bool Seen);
