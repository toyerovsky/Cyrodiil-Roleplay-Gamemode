namespace Roleplay.Jobs.Server;

/// <summary>What a hit does in a job: its damage by the attack and the character's Strength, how soon one may follow
/// another, and how much it wears the tool. <see cref="WoodcuttingConfig"/> and <see cref="MiningConfig"/> each set
/// their own.</summary>
public interface IHitRules
{
    int NormalDamage { get; }
    int PowerDamage { get; }
    int NormalStrengthBonus { get; }
    int NormalStrengthStep { get; }
    int NormalStrengthFrom { get; }
    int PowerStrengthBonus { get; }
    int PowerStrengthStep { get; }
    int PowerStrengthFrom { get; }
    int NormalTierBonus { get; }
    int PowerTierBonus { get; }
    float MinSecondsBetweenHits { get; }
    float MinSecondsBetweenPowerHits { get; }
    float WearPerDamage { get; }
    float DullBelow { get; }
    float MaxGlanceChance { get; }
}
