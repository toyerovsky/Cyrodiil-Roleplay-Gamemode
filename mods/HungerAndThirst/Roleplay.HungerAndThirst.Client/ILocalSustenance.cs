using ReadyM.SDK.Archetypes.Core;
using ReadyM.SDK.Attributes;
using Roleplay.HungerAndThirst.Common;

namespace Roleplay.HungerAndThirst.Client;

/// <summary>
/// The local player's hunger and thirst for other client mods: what to draw in a UI of their own, and the satchel's
/// actions (eat, drink, feed another player). Inject it; it is registered by this mod's entry point, so a mod using it
/// lists <c>Roleplay.HungerAndThirst</c> in its manifest's dependencies.
/// </summary>
/// <remarks>
/// <para>The meters are the same smoothed values the mod's own bars show: counted down between server updates, with a
/// meal on its way already in. Read them every frame if you like; nothing is sent.</para>
/// <para>Other players' meters replicate too, as <c>player.Satiety</c> and <c>player.Hydration</c> on any
/// <see cref="Player"/> (the <see cref="Sustenance"/> mixin), whole numbers without the smoothing.</para>
/// <para>Game thread only, like the rest of the SDK.</para>
/// </remarks>
public interface ILocalSustenance
{
    /// <summary>The server's values have arrived. Until then the meters read 0.</summary>
    bool Ready { get; }

    /// <summary>0 (starving) to <see cref="SustenanceScale.Max"/> (full).</summary>
    double Satiety { get; }

    /// <summary>0 (parched) to <see cref="SustenanceScale.Max"/> (full).</summary>
    double Hydration { get; }

    /// <summary>How fast satiety falls right now, in points per real minute (faster running, swimming, fighting).</summary>
    float SatietyPerMinute { get; }

    float HydrationPerMinute { get; }

    SustenanceBand HungerBand { get; }

    SustenanceBand ThirstBand { get; }

    /// <summary>The character swims, so <see cref="DrinkWater"/> works.</summary>
    bool InWater { get; }

    /// <summary>The server's food table. Empty until it has been sent.</summary>
    IReadOnlyCollection<Food> Foods { get; }

    /// <summary>The foods and drinks of the table the player carries.</summary>
    IReadOnlyList<CarriedFood> Carried();

    /// <summary>Eats or drinks one from the pack. The answer comes as <see cref="Meal"/> or <see cref="MealRefused"/>.</summary>
    void Eat(uint formId);

    /// <summary>A sip from the water the player swims in.</summary>
    void DrinkWater();

    /// <summary>Gives <paramref name="eater"/> one from the player's pack. They must stand near (the server's feedRange).</summary>
    void Feed(Player eater, uint formId);

    /// <summary>The mod's satchel (Alt+H) is open. Set it to open or close it.</summary>
    bool SatchelOpen { get; set; }

    /// <summary>False hides the mod's own bars, for a mod that draws hunger and thirst itself. True by default.</summary>
    bool HudEnabled { get; set; }

    /// <summary>Somebody nearby ate or drank: the player, someone feeding them, or anyone else in the area.</summary>
    event Action<MealNews>? Meal;

    /// <summary>The player's meal or feeding didn't happen. <see cref="MealRefusals.Describe"/> has the mod's words.</summary>
    event Action<MealRefusal>? MealRefused;
}

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
