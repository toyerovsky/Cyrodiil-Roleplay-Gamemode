namespace CyrodiilRP.Server.Fishing;

// The fishing page: the line's stage, counters the page plays a cue on when they grow, the strike's window, the fight
// while a fish is on, the rod, the bait, the skill, and the last catch or loss.
internal sealed record FishingView(
    FishingStage Stage,
    int Nibbles,
    int Bites,
    int StrikeWindowMs,
    FishingFightView? Fight,
    FishingRodView Rod,
    FishingBaitView? Bait,
    FishingSkillView Skill,
    FishingOutcomeView? Outcome);
