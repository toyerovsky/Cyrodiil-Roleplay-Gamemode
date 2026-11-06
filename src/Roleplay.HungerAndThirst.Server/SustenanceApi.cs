using ReadyM.Api.Idents;
using ReadyM.SDK.Archetypes.Core;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Events;
using ReadyM.SDK.Server.Entities;
using Roleplay.HungerAndThirst.Common;

namespace Roleplay.HungerAndThirst.Server;

/// <summary>The <see cref="ISustenance"/> other mods get, over the service that keeps the meters.</summary>
[Service]
public sealed partial class SustenanceApi(SustenanceService sustenance, FoodTable foods, INetworkedEvents events, IEntities entities)
    : ISustenance
{
    public IReadOnlyCollection<FoodItem> Foods => foods.All;

    public bool TryGetMeters(PlayerId player, out SustenanceMeters meters) => sustenance.TryGetMeters(player, out meters);

    public IReadOnlyList<CarriedFood> CarriedBy(PlayerId player) => sustenance.CarriedBy(player);

    public MealRefusal? Check(PlayerId feeder, PlayerId eater, uint formId) => sustenance.Check(feeder, eater, formId);

    public MealRefusal? Feed(PlayerId feeder, PlayerId eater, uint formId)
    {
        if (sustenance.Check(feeder, eater, formId) is { } refusal)
            return refusal;
        if (!entities.TryLookup(feeder, out Player giver) || !entities.TryLookup(eater, out Player taker))
            return MealRefusal.NotInWorld;
        events.RaiseAs(feeder, new MealEvent { Feeder = giver, Eater = taker, FormId = formId });
        return null;
    }

    public void Refill(PlayerId player) => sustenance.Refill(player);
}
