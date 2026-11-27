using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Progression;

[Service]
internal sealed partial class EnemyRanks(RpConfig config)
{
    private readonly HashSet<uint> _elites = config.Progression.Elites.Select(ProgressionSettings.ParseForm).ToHashSet();
    private readonly HashSet<uint> _bosses = config.Progression.Bosses.Select(ProgressionSettings.ParseForm).ToHashSet();
    private readonly HashSet<uint> _rares = config.Progression.Rares.Select(ProgressionSettings.ParseForm).ToHashSet();

    public double XpMultiplier(uint form) => _rares.Contains(form) ? config.Progression.RareMult
        : _bosses.Contains(form) ? config.Progression.BossMult : _elites.Contains(form) ? config.Progression.EliteMult : 1;

    public double HealthMultiplier(uint form) => _bosses.Contains(form) ? config.Zones.BossHealth
        : _elites.Contains(form) ? config.Zones.EliteHealth : 1;

    public double DamageMultiplier(uint form) => _bosses.Contains(form) ? config.Zones.BossDamage
        : _elites.Contains(form) ? config.Zones.EliteDamage : 1;
}
