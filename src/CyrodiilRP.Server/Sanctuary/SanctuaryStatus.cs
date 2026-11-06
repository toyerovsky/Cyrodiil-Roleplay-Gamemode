namespace CyrodiilRP.Server.Sanctuary;

// One player's place in the Sanctuary as /sanctuary and the menu show it. Area: the name of the area while inside, null
// outside. SecondsLeft: of the aggressor's streak, 0 when none.
internal sealed record SanctuaryStatus(bool Enabled, bool Inside, string? Area, bool Aggressor, int SecondsLeft);
