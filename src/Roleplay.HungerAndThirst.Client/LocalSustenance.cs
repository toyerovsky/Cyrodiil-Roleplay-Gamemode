using ReadyM.SDK.Archetypes.Core;
using ReadyM.SDK.Attributes;
using Roleplay.HungerAndThirst.Common;

namespace Roleplay.HungerAndThirst.Client;

/// <summary>The <see cref="ILocalSustenance"/> other mods get, over the mod's state and page.</summary>
[Service]
public sealed partial class LocalSustenance(SustenanceState state, SustenancePage page) : ILocalSustenance
{
    public bool Ready => state.Ready;

    public double Satiety => state.Satiety;

    public double Hydration => state.Hydration;

    public float SatietyPerMinute => state.SatietyRate;

    public float HydrationPerMinute => state.HydrationRate;

    public SustenanceBand HungerBand => SustenanceScale.BandOf(state.Satiety);

    public SustenanceBand ThirstBand => SustenanceScale.BandOf(state.Hydration);

    public bool InWater => state.InWater;

    public IReadOnlyCollection<Food> Foods => state.Foods;

    public IReadOnlyList<CarriedFood> Carried() => state.Carried();

    public void Eat(uint formId) => state.Eat(formId);

    public void DrinkWater() => state.DrinkWater();

    public void Feed(Player eater, uint formId) => state.Feed(eater, formId);

    public bool SatchelOpen
    {
        get => page.SatchelOpen;
        set => page.SatchelOpen = value;
    }

    public bool HudEnabled
    {
        get => page.HudEnabled;
        set => page.HudEnabled = value;
    }

    public event Action<MealNews>? Meal
    {
        add => state.Meal += value;
        remove => state.Meal -= value;
    }

    public event Action<MealRefusal>? MealRefused
    {
        add => state.MealRefused += value;
        remove => state.MealRefused -= value;
    }
}
