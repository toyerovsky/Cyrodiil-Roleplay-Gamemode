namespace CyrodiilRP.Server.PlayerMenu;

// A player in play who knows the reader's name: their number, and their name as the reader sees it.
internal sealed record MenuKnownView(int Number, string Name);
