using CyrodiilRP.Data;
using CyrodiilRP.Data.Progression;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Network;
using CyrodiilRP.Server.Players;
using Microsoft.Extensions.Logging;
using OblivionMP.SDK.Common.Archetypes;
using ReadyM.Api.Idents;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Server.Entities;

namespace CyrodiilRP.Server.Progression;

[Service]
internal sealed partial class Progression(
    OnlinePlayers online, ProgressionRegistry registry, Persistence persistence, Fire fire, RpConfig config,
    RpcHandlers rpc, Localizer texts, IEntities entities, SystemClock clock, Timers timers, ILogger logger, ProgressionHud hud) : IProgression
{
    private const string LoadJob = "progression.load";
    private const string SaveJob = "progression.save";
    private readonly HashSet<OnlinePlayer> _loading = [];
    private readonly HashSet<OnlinePlayer> _syncPending = [];
    private ProgressionSettings Settings => config.Progression;

    private void Start()
    {
        online.Loaded += Load;
        online.Playing += Load;
        online.LocaleChanged += Show;
        online.Left += player =>
        {
            registry.Remove(player);
            _loading.Remove(player);
            _syncPending.Remove(player);
        };
        timers.RunEvery("progression.sync", TimeSpan.FromSeconds(1), SyncPending);
    }

    public bool Grant(PlayerId player, ProgressionSource source, int xp) =>
        online.TryGet(player, out var found) && Grant(found, source, xp);

    public bool Grant(OnlinePlayer player, ProgressionSource source, int xp)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(xp);
        if (!Settings.Enabled || !player.IsLive || !player.IsPlaying || registry.Find(player) is not { } before)
            return false;
        if (xp == 0 || before.Level >= Settings.MaxLevel)
            return true;

        var state = ProgressionRules.Grant(before, source, xp, Settings, clock.UtcNow);
        Remember(player, state);
        if (state.Level > before.Level)
        {
            var character = player.Character!;
            var played = character.PlayedSeconds + Math.Max(0, (long)(clock.UtcNow - player.PlayCountedUntil).TotalSeconds);
            for (var level = before.Level + 1; level <= state.Level; level++)
                logger.LogInformation("[CyrodiilRP] progression.level character={Character} level={Level} playedSeconds={PlayedSeconds} source={Source}",
                    character.Ref, level, played, source);
            rpc.SendCenterMessage(player.Id, texts.Get(player.Locale, ProgressionText.Level(state.Level)));
            _syncPending.Add(player);
            TrySync(player);
        }
        return true;
    }

    public void SetLevel(OnlinePlayer player, int level)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(level, Settings.MaxLevel);
        var state = registry.Find(player) ?? throw new InvalidOperationException("Progression is not loaded.");
        Remember(player, state with { Level = level, Xp = 0, ReachedAt = clock.UtcNow,
            PerkPoints = checked((level - 1) * Settings.PerkPointsPerLevel) });
        _syncPending.Add(player);
        TrySync(player);
    }

    public void RecordSkills(OnlinePlayer player, ProgressionState state, int xp)
    {
        var after = state with { SkillPeaks = state.SkillPeaks.ToArray() };
        if (xp == 0 || state.Level >= Settings.MaxLevel)
            Remember(player, after);
        else
        {
            registry.Set(player, after);
            Grant(player, ProgressionSource.Skill, xp);
        }
    }

    public void Remember(OnlinePlayer player, ProgressionState state)
    {
        registry.Set(player, state);
        var row = new CharacterProgression
        {
            CharacterId = state.CharacterId, Level = state.Level, Xp = state.Xp, PerkPoints = state.PerkPoints,
            ReachedAt = state.ReachedAt, GatheringSince = state.GatheringSince, GatheringXp = state.GatheringXp,
            SkillPeaks = state.SkillPeaks.ToArray(),
        };
        fire.Run(SaveJob, () => persistence.Writer.EnqueueAsync(SaveJob, (db, ct) => ProgressionBook.SaveAsync(db, row, ct)));
        Show(player);
    }

    private void Load(OnlinePlayer player)
    {
        if (player.Character is not { } character || registry.Find(player) is not null || !_loading.Add(player))
        {
            Show(player);
            return;
        }
        var characterId = character.Id;
        fire.Run(LoadJob, async () =>
        {
            try
            {
                var row = await persistence.Writer.EnqueueAsync(LoadJob, (db, ct) => ProgressionBook.FindAsync(db, characterId, ct));
                if (!player.IsLive || player.Character?.Id != characterId)
                    return;
                if (row is not null && (row.Level < 1 || row.Xp < 0 || row.PerkPoints < 0
                    || row.GatheringXp < 0 || row.SkillPeaks.Length != ProgressionRules.SkillSlots || row.SkillPeaks.Any(x => x < -1)))
                    throw new InvalidOperationException($"Invalid progression row for {character.Ref}.");
                var state = row is null
                    ? new ProgressionState(characterId, 1, 0, 0, clock.UtcNow, clock.UtcNow, 0,
                        Enumerable.Repeat(-1, ProgressionRules.SkillSlots).ToArray())
                    : new ProgressionState(characterId, row.Level, row.Xp, row.PerkPoints, row.ReachedAt,
                        row.GatheringSince, row.GatheringXp, row.SkillPeaks);
                if (state.Level > Settings.MaxLevel)
                {
                    logger.LogWarning("[CyrodiilRP] Progression {Character} level {Level} exceeds configured max {Max}, clamping.", character.Ref, state.Level, Settings.MaxLevel);
                    state = state with { Level = Settings.MaxLevel, Xp = 0 };
                }
                Remember(player, ProgressionRules.Gain(state, 0, Settings, clock.UtcNow));
                _syncPending.Add(player);
                TrySync(player);
            }
            finally
            {
                _loading.Remove(player);
            }
        });
    }

    private void Show(OnlinePlayer player)
    {
        var state = registry.Find(player);
        var view = new ProgressionView(Settings.Enabled && player.IsPlaying && state is not null,
            state?.Level ?? 1, state?.Xp ?? 0, state is not null && state.Level < Settings.MaxLevel ? Settings.XpToNext[state.Level - 1] : 0,
            state?.PerkPoints ?? 0);
        hud.Show(player, view);
    }

    private void SyncPending()
    {
        foreach (var player in _syncPending.ToArray())
            TrySync(player);
    }

    private void TrySync(OnlinePlayer player)
    {
        if (!Settings.Enabled || !Settings.SyncEngineLevel)
        {
            _syncPending.Remove(player);
            return;
        }
        if (!player.IsPlaying || registry.Find(player) is not { } state || !entities.TryLookup(player.Id, out MainCharacter main) || main.HpBase <= 0)
            return;
        var engine = (ushort)ProgressionRules.EngineLevel(state.Level);
        var before = main.Level;
        main.Level = engine;
        _syncPending.Remove(player);
        logger.LogInformation("[CyrodiilRP] progression.engine_level character={Character} ourLevel={Level} before={Before} engine={Engine} unverified=true",
            player.Character!.Ref, state.Level, before, engine);
    }
}
