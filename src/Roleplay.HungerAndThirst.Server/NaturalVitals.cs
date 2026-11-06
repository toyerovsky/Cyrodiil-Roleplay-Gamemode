using OblivionMP.SDK.Common.Statuses;

namespace Roleplay.HungerAndThirst.Server;

/// <summary>The character's own maximum of each vital, before any of the mod's effects.</summary>
public readonly record struct NaturalVitals(float Health, float Magicka, float Fatigue)
{
    public float Of(Stat vital) => vital switch
    {
        Stat.Health => Health,
        Stat.Magicka => Magicka,
        _ => Fatigue,
    };
}
