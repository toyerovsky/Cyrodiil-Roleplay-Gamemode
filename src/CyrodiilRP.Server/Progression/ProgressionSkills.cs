using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Players;
using CyrodiilRP.Server.Specializations;
using Microsoft.Extensions.Logging;
using OblivionMP.SDK.Common.Archetypes;
using OblivionMP.SDK.Common.Statuses;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Server.Entities;

namespace CyrodiilRP.Server.Progression;

[Service]
internal sealed partial class ProgressionSkills(
    Progression progression, ProgressionRegistry registry, OnlinePlayers online, CharacterSpecializations specializations,
    IEntities entities, RpConfig config, Timers timers, ILogger logger)
{
    private readonly HashSet<OnlinePlayer> _warned = [];
    private readonly SkillObservationRegistry _observations = new();

    private void Start()
    {
        timers.RunEvery("progression.skills", TimeSpan.FromSeconds(1), Watch);
        online.Left += player =>
        {
            _warned.Remove(player);
            _observations.Remove(player);
        };
    }

    private void Watch()
    {
        if (!config.Progression.Enabled)
            return;
        foreach (var player in online.All)
        {
            if (!player.IsPlaying || registry.Find(player) is not { } state || specializations.Find(player) is not { } chosen
                || !entities.TryLookup(player.Id, out MainCharacter main) || main.HpBase <= 0)
                continue;
            var majors = chosen.Row is { } row
                ? SpecializationRules.FindMajorSkills(SpecializationRules.FindProfile(row.Specialization)) : Array.Empty<Stat>();
            if (chosen.Row is null && _warned.Add(player))
                logger.LogWarning("[CyrodiilRP] progression.skills {Character}: SDK does not expose original class majors, using minor XP until specialization is chosen.", player.Character!.Ref);
            var peaks = state.SkillPeaks.ToArray();
            var values = new int[ProgressionRules.SkillSlots];
            var complete = true;
            foreach (var skill in SpecializationRules.Skills)
            {
                if (main.TryGetBaseSkillAttributeValue((byte)skill, out var value) && float.IsFinite(value) && value >= 0)
                    values[(int)skill] = (int)MathF.Floor(value);
                else
                    complete = false;
            }
            if (!complete || (SpecializationRules.Skills.All(skill => peaks[(int)skill] < 0) && !_observations.IsStable(player, values)))
                continue;
            var xp = 0;
            var changed = false;
            foreach (var skill in SpecializationRules.Skills)
            {
                var index = (int)skill;
                var level = values[index];
                if (level <= peaks[index])
                    continue;
                if (peaks[index] >= 0)
                    xp += ProgressionRules.SkillXp(peaks[index], level, majors.Contains(skill), config.Progression);
                peaks[index] = level;
                changed = true;
            }
            if (!changed)
                continue;
            progression.RecordSkills(player, state with { SkillPeaks = peaks }, xp);
        }
    }
}
