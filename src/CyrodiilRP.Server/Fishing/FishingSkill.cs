namespace CyrodiilRP.Server.Fishing;

// A character's fishing level and the xp it has towards the next.
internal sealed record FishingSkill(int Level, int Xp)
{
    public static readonly FishingSkill Untrained = new(FishingSkillRules.LowestLevel, 0);

    public int XpNext => FishingSkillRules.XpToNext(Level);

    // Each level the xp reaches, as many as it reaches. The highest level keeps no xp.
    public FishingSkill Gain(int xp)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(xp);
        var level = Level;
        var have = Xp + xp;
        while (level < FishingSkillRules.HighestLevel && have >= FishingSkillRules.XpToNext(level))
        {
            have -= FishingSkillRules.XpToNext(level);
            level++;
        }

        return new FishingSkill(level, level >= FishingSkillRules.HighestLevel ? 0 : have);
    }
}
