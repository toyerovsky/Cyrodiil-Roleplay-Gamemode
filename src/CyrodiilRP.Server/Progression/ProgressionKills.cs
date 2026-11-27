using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Party;
using CyrodiilRP.Server.Players;
using CyrodiilRP.Server.Zones;
using Microsoft.Extensions.Logging;
using OblivionMP.SDK.Common.Archetypes;
using OblivionMP.SDK.Common.Archetypes.Mixins;
using OblivionMP.SDK.Common.Events;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Entities;
using ReadyM.SDK.Events;

namespace CyrodiilRP.Server.Progression;

[Service]
internal sealed partial class ProgressionKills(
    Progression progression, ProgressionRegistry registry, OnlinePlayers online, Parties parties,
    ZoneResolver zones, EnemyRanks ranks, RpConfig config, Timers timers, SystemClock clock, ILogger logger)
{
    private readonly NpcDeathRegistry _deaths = new();

    private void Start() => timers.RunEvery("progression.deaths", TimeSpan.FromSeconds(30), _deaths.ForgetGone);

    [EventHandler(Stage.Filter)]
    public void BeforeDamage(in HealthChangedEvent change, EventContext context)
    {
        if (config.Progression.Enabled && change.Amount < 0 && change.Victim.TryAs(out Npc npc))
            _deaths.Before(EntityHandle.Of(npc), npc.Hp);
    }

    [EventHandler(Stage.Observe)]
    public void AfterDamage(in HealthChangedEvent change, EventContext context)
    {
        if (!config.Progression.Enabled || change.Amount >= 0 || !change.Victim.TryAs(out Npc npc))
            return;
        var killer = change.Attacker is { } attacker && attacker.TryAs(out MainCharacter main)
            && online.TryGet(main.PlayerId, out var player) ? player : null;
        if (!_deaths.Observe(EntityHandle.Of(npc), killer is { IsPlaying: true }, npc.Hp, clock.MonotonicMs, out var first))
            return;
        var resolution = zones.TryLocate(npc, out var cell) ? zones.Resolve(cell) : null;
        var enemyLevel = resolution?.Level ?? npc.Level * ProgressionRules.EngineScale;
        if (first is { } started)
            logger.LogInformation("[CyrodiilRP] progression.kill npc={Npc:X8} base={Base:X8} ttkMs={TtkMs} zone={Zone} enemyLevel={EnemyLevel}",
                npc.RefId, npc.BaseId.FormId.Value, clock.MonotonicMs - started, resolution?.Zone.Id, enemyLevel);
        if (killer is not { IsPlaying: true } || npc.Level == 0 || npc.TryAs(out SummonedBy _) || registry.Find(killer) is not { } state)
            return;
        var members = parties.MembersOf(killer).Where(member => member.IsLive && member.IsPlaying && registry.Find(member) is not null
            && (member == killer || zones.TryLocate(member.Id, out var at) && InRange(cell, at, config.Progression.PartyRangeCells))).OrderBy(x => x.Number).ToList();
        var pool = ProgressionRules.KillXp(enemyLevel, state.Level, ranks.XpMultiplier(npc.BaseId.FormId.Value), config.Progression);
        var shares = ProgressionRules.Split(pool, members.Count, config.Progression);
        for (var index = 0; index < members.Count; index++)
            progression.Grant(members[index], ProgressionSource.Kill, shares[index]);
    }

    internal static bool InRange(ZoneCell origin, ZoneCell other, int range) => origin.Area == other.Area && origin.Kind == other.Kind
        && Math.Pow((double)origin.X - other.X, 2) + Math.Pow((double)origin.Y - other.Y, 2) <= (double)range * range;
}
