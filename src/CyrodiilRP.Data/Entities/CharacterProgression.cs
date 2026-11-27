namespace CyrodiilRP.Data;

public sealed class CharacterProgression
{
    public long CharacterId { get; set; }
    public int Level { get; set; } = 1;
    public int Xp { get; set; }
    public int PerkPoints { get; set; }
    public DateTimeOffset ReachedAt { get; set; }
    public DateTimeOffset GatheringSince { get; set; }
    public int GatheringXp { get; set; }
    public int[] SkillPeaks { get; set; } = [];
}
