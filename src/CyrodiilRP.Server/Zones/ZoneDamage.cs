using OblivionMP.SDK.Common.Archetypes;
using OblivionMP.SDK.Common.Events;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Events;

namespace CyrodiilRP.Server.Zones;

[Service]
internal sealed partial class ZoneDamage(ZoneResolver resolver, RpConfig config)
{
    [EventHandler(Stage.Filter)]
    public void AdjustDamage(ref HealthChangedEvent change, EventContext context)
    {
        if (change.Amount >= 0 || change.Attacker is not { } attacker)
            return;

        if (attacker.TryAs(out Npc strikingNpc) && change.Victim.TryAs(out MainCharacter struckPlayer))
            change.Amount = Adjust(change.Amount, true, strikingNpc, struckPlayer);
        else if (attacker.TryAs(out MainCharacter strikingPlayer) && change.Victim.TryAs(out Npc struckNpc))
            change.Amount = Adjust(change.Amount, false, struckNpc, strikingPlayer);
    }

    private float Adjust(float amount, bool npcAttacks, Npc npc, MainCharacter player)
    {
        return resolver.TryLocate(npc, out var cell) && resolver.Resolve(cell) is { } resolution
            ? ZoneLevels.Damage(amount, npcAttacks, resolution.Level, player.Level, config.Zones)
            : amount;
    }
}
