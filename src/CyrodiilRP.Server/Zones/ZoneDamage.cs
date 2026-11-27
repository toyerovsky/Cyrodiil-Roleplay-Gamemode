using OblivionMP.SDK.Common.Archetypes;
using OblivionMP.SDK.Common.Events;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Events;
using CyrodiilRP.Server.Players;
using CyrodiilRP.Server.Progression;

namespace CyrodiilRP.Server.Zones;

[Service]
internal sealed partial class ZoneDamage(ZoneResolver resolver, RpConfig config, OnlinePlayers online, ProgressionRegistry progression, EnemyRanks ranks)
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
        if (!config.Zones.Enabled || !resolver.TryLocate(npc, out var cell) || resolver.Resolve(cell) is not { } resolution
            || !online.TryGet(player.PlayerId, out var member))
            return amount;
        var players = online.All.Count(other => other.IsLive && other.IsPlaying && resolver.TryLocate(other.Id, out var at) && at == cell);
        var adjusted = ZoneLevels.Damage(amount, npcAttacks, resolution.Level, progression.LevelOf(member), config.Zones);
        return ZoneLevels.Strength(adjusted, npcAttacks, resolution.Zone.NpcHealth * ranks.HealthMultiplier(npc.BaseId.FormId.Value),
            resolution.Zone.NpcDamage * ranks.DamageMultiplier(npc.BaseId.FormId.Value), players, config.Zones);
    }
}
