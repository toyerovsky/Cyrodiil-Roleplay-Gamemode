namespace CyrodiilRP.Server.Fishing;

// What a catch brought the angler: xp, the level it reached (null for none), a new best of its species, and whether it
// was the first of them.
internal sealed record CatchGain(int Xp, int? LevelUp, bool Record, bool First);
