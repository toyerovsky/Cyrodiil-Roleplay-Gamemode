namespace CyrodiilRP.Server.Fishing;

// What a cast sets for its line: the fight of what will bite and its size's share, the wait for the bite as a share of
// FishingRules' (the skill, the rod, the bait), the strike's window, the strain the line takes (the rod's line x the
// skill's tolerance), the rod's reach, how far out the float lies, how near the angler it may be pulled in over open
// water, and what a twitch of the bait on the hook does. Centimetres.
internal sealed record LineCast(
    FishFight Fight,
    double SizeShare,
    double WaitFactor,
    TimeSpan StrikeWindow,
    double LineStrength,
    float ReachCm,
    float DistanceCm,
    float NearestCm,
    BaitTwitch Twitch);
