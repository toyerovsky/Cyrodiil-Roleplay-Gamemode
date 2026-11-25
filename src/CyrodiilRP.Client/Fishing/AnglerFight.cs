namespace CyrodiilRP.Client.Fishing;

// The line's tension against where its red begins, whether the server has the angler reeling, and the rod's lean it took.
internal sealed record AnglerFight(float Tension, float TensionWarn, bool Reeling, int Lean);
