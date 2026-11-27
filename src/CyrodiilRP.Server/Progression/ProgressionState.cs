namespace CyrodiilRP.Server.Progression;

internal sealed record ProgressionState(
    long CharacterId, int Level, int Xp, int PerkPoints, DateTimeOffset ReachedAt,
    DateTimeOffset GatheringSince, int GatheringXp, int[] SkillPeaks);
