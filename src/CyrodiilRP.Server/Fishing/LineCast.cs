namespace CyrodiilRP.Server.Fishing;

// What a cast sets for its line: the fight of what will bite and its size's share, the wait for the bite as a share of
// FishingRules' (the skill, the rod, the bait), the strike's window, the strain the line takes (the rod's line x the
// skill's tolerance), the rod's reach and how far out the float lies. Centimetres.
internal sealed record LineCast(
    FishFight Fight,
    double SizeShare,
    double WaitFactor,
    TimeSpan StrikeWindow,
    double LineStrength,
    float ReachCm,
    float DistanceCm);
