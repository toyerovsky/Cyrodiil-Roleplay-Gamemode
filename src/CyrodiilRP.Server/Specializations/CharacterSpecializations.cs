using CyrodiilRP.Data;
using CyrodiilRP.Data.Specializations;
using CyrodiilRP.Server.Characters;
using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Players;
using Microsoft.Extensions.Logging;
using CyrodiilRP.Server.Admin;
using OblivionMP.SDK.Common.Archetypes;
using OblivionMP.SDK.Common.Skills;
using OblivionMP.SDK.Common.Statuses;
using OblivionMP.SDK.Serverside.Skills;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Server.Entities;

namespace CyrodiilRP.Server.Specializations;

// The specialization of each player online, read at the join, and the skill caps it puts on them. A character who has
// not chosen one gets the unchosen caps, so nothing is levelled past them before the choice.
[Service]
internal sealed partial class CharacterSpecializations(
    OnlinePlayers online,
    ISkillCaps skillCaps,
    Persistence persistence,
    RpConfig config,
    RpChat chat,
    Fire fire,
    Callbacks callbacks,
    IEntities entities,
    ILogger logger)
{
    private const string LoadJob = "specializations.load";
    private const string ChangedEvent = "specializations.changed";

    private readonly Dictionary<OnlinePlayer, ChosenSpecialization> _chosen = [];

    // A player's specialization was read or chosen: what shows it may show it again.
    public event Action<OnlinePlayer>? Changed;

    private void Start()
    {
        online.Loaded += OnLoaded;
        online.Left += player => _chosen.Remove(player);
        online.LocaleChanged += ApplyCaps;
    }

    // Null while the player's character is not read yet.
    public ChosenSpecialization? Find(OnlinePlayer player) =>
        _chosen.TryGetValue(player, out var chosen) && player.Character?.Id == chosen.CharacterId ? chosen : null;

    // The name of the character's specialization, null before the first choice or while it is not read yet.
    public Phrase? FindName(OnlinePlayer player) =>
        Find(player)?.Row is { } row ? SpecializationRules.FindName(row.Specialization) : null;

    // Every attribute and skill with its value now and its cap, null while the character is not read yet.
    public IReadOnlyList<AdminStatView>? BuildAdminStats(OnlinePlayer player)
    {
        if (Find(player) is not { } chosen || FindCaps(player) is not { } caps)
            return null;

        var inWorld = entities.TryLookup(player.Id, out MainCharacter main);
        return SpecializationRules.Attributes.Concat(SpecializationRules.Skills)
            .Select(stat => new AdminStatView(
                stat.ToString(),
                stat.DisplayName(),
                inWorld && main.TryGetBaseSkillAttributeValue((byte)stat, out var value) ? (int)MathF.Floor(value) : null,
                caps.FindCap(stat),
                chosen.Overrides.ContainsKey(stat)))
            .ToList();
    }

    // The caps the player's game holds them to now, null while the character is not read yet.
    public SkillCaps? FindCaps(OnlinePlayer player) => skillCaps.Find(player.Id);

    // On the writer, so a read and a choice come back in the order they were asked.
    public async Task RefreshAsync(OnlinePlayer player, long characterId)
    {
        var row = await persistence.Writer.EnqueueAsync(LoadJob, (db, ct) => SpecializationBook.FindAsync(db, characterId, ct));
        var overrides = await persistence.Writer.EnqueueAsync(LoadJob, (db, ct) => SpecializationBook.LoadOverridesAsync(db, characterId, ct));
        if (!player.IsLive || player.Character?.Id != characterId)
            return;

        Keep(player, new ChosenSpecialization(characterId, row, ReadOverrides(characterId, overrides)));
    }

    // A choice that was written.
    public void Remember(OnlinePlayer player, long characterId, CharacterSpecialization row)
    {
        var overrides = Find(player)?.Overrides ?? SpecializationRules.NoOverrides;
        Keep(player, new ChosenSpecialization(characterId, row, overrides));
    }

    // What the staff wrote: a specialization or none, or a cap in place of the specialization's or taken off.
    public void RememberStaffChange(OnlinePlayer player, long characterId, CharacterSpecialization? row, IReadOnlyDictionary<Stat, int> overrides) =>
        Keep(player, new ChosenSpecialization(characterId, row, overrides));

    private void Keep(OnlinePlayer player, ChosenSpecialization chosen)
    {
        _chosen[player] = chosen;
        ApplyCaps(player);
        callbacks.Raise(ChangedEvent, Changed, player);
    }

    private Dictionary<Stat, int> ReadOverrides(long characterId, IEnumerable<CharacterCapOverride> rows)
    {
        var overrides = new Dictionary<Stat, int>();
        foreach (var row in rows)
        {
            if (SpecializationRules.TryReadStat(row.Stat, out var stat))
                overrides[stat] = row.Cap;
            else
                logger.LogError("[CyrodiilRP] {Character} has a cap override on {Stat}, which is no attribute or skill. It is left out.",
                    CharacterRecord.FormatRef(characterId), row.Stat);
        }

        return overrides;
    }

    private void OnLoaded(OnlinePlayer player)
    {
        if (player.Character is not { } character)
            return;

        var characterId = character.Id;
        fire.Run(LoadJob, async () =>
        {
            try
            {
                await RefreshAsync(player, characterId);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "[CyrodiilRP] The specialization of {Character} could not be read.", CharacterRecord.FormatRef(characterId));
            }
        });
    }

    private void ApplyCaps(OnlinePlayer player)
    {
        if (Find(player) is not { } chosen)
            return;

        var messages = new SkillCapMessages(
            TrainingRefused: chat.Translate(player, SpecializationText.CapTraining(StatToken, CapToken)),
            BookRefused: chat.Translate(player, SpecializationText.CapBook(StatToken, CapToken)),
            PotionRefused: chat.Translate(player, SpecializationText.CapPotion(StatToken)),
            HeldAtCap: chat.Translate(player, SpecializationText.CapHeld(StatToken, CapToken)));
        skillCaps.Set(player.Id, SpecializationRules.BuildCaps(chosen.Row?.Specialization, config.Specializations, messages, chosen.Overrides));
    }

    // Left in the texts for the player's client, which fills them in.
    private const string StatToken = "{stat}";
    private const string CapToken = "{cap}";
}
