using System.Collections.Immutable;

namespace CyrodiilRP.Server.Fishing;

// A character's fishing while it plays: the skill, the fish landed, the bait chosen and the best of each species.
internal sealed record AnglerProgress(long CharacterId, FishingSkill Skill, int Catches, BaitKind Bait, ImmutableDictionary<FishSpecies, FishRecord> Records)
{
    public static AnglerProgress Fresh(long characterId) =>
        new(characterId, FishingSkill.Untrained, Catches: 0, BaitKind.None, ImmutableDictionary<FishSpecies, FishRecord>.Empty);

    public CatchGain Judge(FishQuarry fish)
    {
        var first = !Records.TryGetValue(fish.Fish.Species, out var best);
        var xp = FishingSkillRules.CatchXp(fish.Fish, fish.Share, fish.Trophy, first);
        return new CatchGain(xp, LevelReached(xp), Record: best is not null && fish.LengthCm > best.BestLengthCm, first);
    }

    public CatchGain JudgeFind() => new(FishingSkillRules.FindXp, LevelReached(FishingSkillRules.FindXp), Record: false, First: false);

    public AnglerProgress AfterCatch(FishQuarry fish, CatchGain gain, DateTimeOffset at)
    {
        var species = fish.Fish.Species;
        var record = Records.TryGetValue(species, out var best) && fish.LengthCm <= best.BestLengthCm
            ? best with { Count = best.Count + 1 }
            : new FishRecord(fish.LengthCm, fish.Grams, (best?.Count ?? 0) + 1, at);
        return this with { Skill = Skill.Gain(gain.Xp), Catches = Catches + 1, Records = Records.SetItem(species, record) };
    }

    public AnglerProgress AfterFind(CatchGain gain) => this with { Skill = Skill.Gain(gain.Xp) };

    private int? LevelReached(int xp) => Skill.Gain(xp).Level is var level && level > Skill.Level ? level : null;
}
