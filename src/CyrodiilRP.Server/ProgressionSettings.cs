using System.Globalization;

namespace CyrodiilRP.Server;

internal sealed class ProgressionSettings
{
    public bool Enabled { get; set; } = true;
    public int MaxLevel { get; set; } = 40;
    public int[] XpToNext { get; set; } =
        [800, 900, 1000, 1100, 2500, 2600, 2700, 2800, 2900, 3000, 3100, 3200, 3400, 4000, 4200, 4400, 4600, 4800, 5000,
         5600, 5800, 6000, 6200, 6400, 6600, 6800, 6900, 7000, 7100, 7200, 7300, 7400, 7500, 8200, 8500, 8800, 9100, 9400, 9700];
    public int KillXp { get; set; } = 100;
    public double HigherPerLevel { get; set; } = 0.05;
    public double HigherMax { get; set; } = 0.25;
    public double LowerPerLevel { get; set; } = 0.10;
    public int GreyAt { get; set; } = 10;
    public double EliteMult { get; set; } = 3;
    public double BossMult { get; set; } = 10;
    public double RareMult { get; set; } = 15;
    public List<string> Elites { get; set; } = [];
    public List<string> Bosses { get; set; } = [];
    public List<string> Rares { get; set; } = [];
    public int PartyRangeCells { get; set; } = 2;
    public double GroupBonusPerMember { get; set; } = 0.10;
    public int MajorSkillXp { get; set; } = 30;
    public int MinorSkillXp { get; set; } = 10;
    public int FishingXp { get; set; } = 20;
    public double RareFishMult { get; set; } = 3;
    public int GatheringXp { get; set; } = 15;
    public int GatheringHourlySoft { get; set; } = 1500;
    public int PerkPointsPerLevel { get; set; } = 1;
    public bool SyncEngineLevel { get; set; } = true;

    public void RequireValid()
    {
        if (MaxLevel is < 1 or > ushort.MaxValue || XpToNext.Length != MaxLevel - 1 || XpToNext.Any(x => x <= 0)
            || new[] { KillXp, MajorSkillXp, MinorSkillXp, FishingXp, GatheringXp, GatheringHourlySoft, PerkPointsPerLevel, PartyRangeCells }.Any(x => x < 0)
            || GreyAt <= 0 || new[] { HigherPerLevel, HigherMax, LowerPerLevel, EliteMult, BossMult, RareMult, RareFishMult, GroupBonusPerMember }
                .Any(x => !double.IsFinite(x) || x < 0))
            throw new InvalidOperationException("Invalid progression XP table, ranges or multipliers.");
        foreach (var form in Elites.Concat(Bosses).Concat(Rares))
            _ = ParseForm(form);
    }

    public static uint ParseForm(string form) => uint.Parse(form.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? form[2..] : form,
        NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
}
