using System.Diagnostics.CodeAnalysis;
using CyrodiilRP.Data;
using CyrodiilRP.Data.Specializations;
using CyrodiilRP.Data.Staff;
using CyrodiilRP.Server.Characters;
using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Players;
using OblivionMP.SDK.Common.Archetypes;
using OblivionMP.SDK.Common.Skills;
using OblivionMP.SDK.Common.Statuses;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Server.Entities;

namespace CyrodiilRP.Server.Specializations;

// The staff's fixes for a character whose specialization went wrong or who cheated: /aspec sets one (or none) for free and
// without a cooldown, /acap sets a cap in place of the specialization's or takes it off, /astat sets a stat's value. The
// specialization and the caps are written first, then held.
[Service]
internal sealed partial class SpecializationCommands(
    CommandRegistry commands,
    CharacterSpecializations specializations,
    IEntities entities,
    Persistence persistence,
    RpChat chat,
    Audit audit,
    Fire fire,
    SystemClock clock)
{
    private const string SpecializationAction = "staff.specialization";
    private const string CapAction = "staff.cap";
    private const string StatAction = "staff.stat";
    private const string NoneWord = "none";
    private const string ClearWord = "clear";
    private const string NameSeparator = ", ";

    private void Start()
    {
        commands.AddStaff(StaffPermissions.Specialization, SpecializationText.CmdSpec, SetSpecialization);
        commands.AddStaff(StaffPermissions.Cap, SpecializationText.CmdCap, SetCap);
        commands.AddStaff(StaffPermissions.Stat, SpecializationText.CmdStat, SetStat);
    }

    private void SetSpecialization(CommandCall call)
    {
        if (!call.TryTarget(0, out var target) || !call.MayPunish(target, self: true) || !TryFindRead(call, target, out var chosen))
            return;

        SpecializationKind? kind;
        var word = call.Words.Count > 1 ? call.Words[1] : string.Empty;
        if (string.Equals(word, NoneWord, StringComparison.OrdinalIgnoreCase))
        {
            kind = null;
        }
        else if (SpecializationRules.TryReadAnyCase(word, out var named))
        {
            kind = named;
        }
        else
        {
            call.Refuse(SpecializationText.StaffUnknown(word, string.Join(NameSeparator, Enum.GetNames<SpecializationKind>())));
            return;
        }

        var characterId = chosen.CharacterId;
        var at = clock.UtcNow;
        var by = call.Sender;
        fire.Run(SpecializationAction, async () =>
        {
            try
            {
                await persistence.Writer.EnqueueAsync(SpecializationAction, (db, ct) => SpecializationBook.SetByStaffAsync(db, characterId, kind, at, ct));
            }
            catch (Exception)
            {
                // The writer logged it.
                if (by.IsLive)
                    call.Refuse(SpecializationText.StaffFailed);

                return;
            }

            var row = kind is { } set
                ? new CharacterSpecialization { CharacterId = characterId, Specialization = set, ChosenAt = chosen.Row?.ChosenAt ?? at, Changes = chosen.Row?.Changes ?? 0 }
                : null;
            if (target.IsLive && target.Character?.Id == characterId)
            {
                specializations.RememberStaffChange(target, characterId, row, specializations.Find(target)?.Overrides ?? chosen.Overrides);
                chat.Notify(target, kind is { } given ? SpecializationText.StaffNoticeSet(SpecializationRules.FindName(given)) : SpecializationText.StaffNoticeCleared);
            }

            audit.Log(SpecializationAction, by.Ref, CharacterRecord.FormatRef(characterId), new SpecializationDetails(kind, chosen.Row?.Specialization));
            if (by.IsLive)
                call.Reply(kind is { } done ? SpecializationText.StaffSet(target.Name, SpecializationRules.FindName(done)) : SpecializationText.StaffCleared(target.Name));
        });
    }

    private void SetCap(CommandCall call)
    {
        if (!call.TryTarget(0, out var target) || !call.MayPunish(target, self: true) || !TryReadStat(call, 1, out var stat)
            || !TryFindRead(call, target, out var chosen))
        {
            return;
        }

        int? cap;
        var word = call.Words.Count > 2 ? call.Words[2] : string.Empty;
        if (string.Equals(word, ClearWord, StringComparison.OrdinalIgnoreCase))
        {
            cap = null;
        }
        else if (call.TryNumber(2, out var number))
        {
            if (number is < 0 or > SkillCaps.MostCap)
            {
                call.Refuse(SpecializationText.StaffCapRange(SkillCaps.MostCap));
                return;
            }

            cap = number;
        }
        else
        {
            return;
        }

        var characterId = chosen.CharacterId;
        var by = call.Sender;
        fire.Run(CapAction, async () =>
        {
            try
            {
                await persistence.Writer.EnqueueAsync(CapAction, (db, ct) => SpecializationBook.SetOverrideAsync(db, characterId, stat.ToString(), cap, ct));
            }
            catch (Exception)
            {
                // The writer logged it.
                if (by.IsLive)
                    call.Refuse(SpecializationText.StaffFailed);

                return;
            }

            if (target.IsLive && target.Character?.Id == characterId && specializations.Find(target) is { } now)
            {
                var overrides = new Dictionary<Stat, int>(now.Overrides);
                if (cap is { } value)
                    overrides[stat] = value;
                else
                    overrides.Remove(stat);

                specializations.RememberStaffChange(target, characterId, now.Row, overrides);
            }

            audit.Log(CapAction, by.Ref, CharacterRecord.FormatRef(characterId), new CapDetails(stat, cap));
            if (by.IsLive)
                call.Reply(cap is { } done ? SpecializationText.StaffCapSet(target.Name, stat.DisplayName(), done) : SpecializationText.StaffCapCleared(target.Name, stat.DisplayName()));
        });
    }

    // The value is the stat as the game shows it now; a cap below it would put it back, so it is refused.
    private void SetStat(CommandCall call)
    {
        if (!call.TryTarget(0, out var target) || !call.MayPunish(target, self: true) || !TryReadStat(call, 1, out var stat)
            || !call.TryNumber(2, out var value))
        {
            return;
        }

        if (value is < 0 or > SkillCaps.MostCap)
        {
            call.Refuse(SpecializationText.StaffValueRange(SkillCaps.MostCap));
            return;
        }

        if (specializations.FindCaps(target)?.FindCap(stat) is { } cap && value > cap)
        {
            call.Refuse(SpecializationText.StaffValueOverCap(stat.DisplayName(), cap));
            return;
        }

        if (!entities.TryLookup(target.Id, out MainCharacter main))
        {
            call.Refuse(SpecializationText.StaffNotInWorld(target.Name));
            return;
        }

        float? before = main.TryGetSkillAttributeValue((byte)stat, out var was) ? was : null;
        main.SetSkillAttribute((byte)stat, value);
        audit.Log(StatAction, call.Sender.Ref, target.Character is { } character ? CharacterRecord.FormatRef(character.Id) : target.Ref,
            new StatDetails(stat, value, before));
        call.Reply(SpecializationText.StaffStatSet(target.Name, stat.DisplayName(), value));
    }

    // A character whose specialization is known: before it is read, a write would race the read.
    private bool TryFindRead(CommandCall call, OnlinePlayer target, [NotNullWhen(true)] out ChosenSpecialization? chosen)
    {
        chosen = specializations.Find(target);
        if (chosen is not null)
            return true;

        call.Refuse(SpecializationText.StaffNotRead(target.Name));
        return false;
    }

    private static bool TryReadStat(CommandCall call, int index, out Stat stat)
    {
        var word = index < call.Words.Count ? call.Words[index] : string.Empty;
        if (SpecializationRules.TryReadStat(word, out stat))
            return true;

        call.Refuse(SpecializationText.StaffUnknownStat(word));
        return false;
    }

    private sealed record SpecializationDetails(SpecializationKind? Specialization, SpecializationKind? Replaced);

    private sealed record CapDetails(Stat Stat, int? Cap);

    private sealed record StatDetails(Stat Stat, int Value, float? Before);
}
