using CyrodiilRP.Common.Fishing;

namespace CyrodiilRP.Server.Fishing;

// The rod fishing now, by its item's name. Broken only for the rest after the fight that broke it.
internal sealed record FishingRodView(string Name, RodTier Tier, RodCondition Condition, int ReachCm);
