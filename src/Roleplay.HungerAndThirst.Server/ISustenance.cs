using ReadyM.Api.Idents;
using Roleplay.HungerAndThirst.Common;

namespace Roleplay.HungerAndThirst.Server;

/// <summary>
/// Hunger and thirst for other server mods: every player's meters, the food table, what a player carries, and feeding
/// one player from another's pack (a roleplay wheel, a healer's job, an NPC innkeeper). Inject it; it is registered by
/// this mod's entry point, so a mod using it lists <c>Roleplay.HungerAndThirst</c> in its manifest's dependencies.
/// </summary>
/// <remarks>
/// <para>Meals go through <see cref="MealEvent"/> either way, so a mod that wants to refuse or react to them, from any
/// source, handles that event rather than wrapping this.</para>
/// <para>Server thread only, like the rest of the server SDK.</para>
/// </remarks>
public interface ISustenance
{
    /// <summary>Everything that can be eaten or drunk, and what it gives.</summary>
    IReadOnlyCollection<FoodItem> Foods { get; }

    /// <summary>A player's meters, rates and tiers. False while the server isn't keeping them (not connected yet).</summary>
    bool TryGetMeters(PlayerId player, out SustenanceMeters meters);

    /// <summary>The foods and drinks of the table a player carries, with how many of each.</summary>
    IReadOnlyList<CarriedFood> CarriedBy(PlayerId player);

    /// <summary>Why <paramref name="feeder"/> can't give <paramref name="eater"/> one <paramref name="formId"/> now,
    /// or null when they can. The same player twice is a meal of one's own; 0 is a sip of water.</summary>
    MealRefusal? Check(PlayerId feeder, PlayerId eater, uint formId);

    /// <summary>
    /// Has <paramref name="feeder"/> give <paramref name="eater"/> one <paramref name="formId"/> from their pack: raises
    /// <see cref="MealEvent"/> as the feeder, so every mod sees it as theirs. Returns this mod's refusal at once, or
    /// null when the event went out; another mod may still refuse it in <c>Stage.Validate</c>.
    /// </summary>
    MealRefusal? Feed(PlayerId feeder, PlayerId eater, uint formId);

    /// <summary>Fills a player's meters to the server's start values, for a mod that gives the player a new
    /// character. The meters are kept per ReadyM account, so without this a new character inherits the last one's.</summary>
    void Refill(PlayerId player);
}
