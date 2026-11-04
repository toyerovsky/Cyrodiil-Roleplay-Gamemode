using System.Diagnostics;
using LiteNetLib;
using Microsoft.Extensions.Logging;
using OblivionMp.Sdk.Common.Archetypes;
using OblivionMp.Sdk.Common.Archetypes.Mixins;
using OblivionMp.Sdk.Common.Statuses;
using OblivionMp.Sdk.Serverside.Items;
using OblivionMp.Sdk.Serverside.Statuses;
using ReadyM.Api.Idents;
using ReadyM.Relay.Common.Oblivion.ECS.Values;
using ReadyM.Relay.Common.Oblivion.Rpc;
using ReadyM.SDK.Server.Events;
using ReadyM.Relay.Server.Sdk.Players;
using ReadyM.SDK.Archetypes.Core;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Server.Entities;
using Roleplay.HungerAndThirst.Common;

namespace Roleplay.HungerAndThirst.Server;

/// <summary>
/// The authority on hunger and thirst. Keeps each connected player's meters with full precision, lets them fall
/// every tick at a rate that depends on what the character is doing, and writes the whole-number view onto the
/// <see cref="Sustenance"/> mixin, which replicates to the clients. Puts the statuses of each hunger and thirst tier
/// on the character (<see cref="IStatuses"/>), and is the only place a meal is applied (<see cref="Feed"/>), whatever
/// asked for it.
/// <para>A character that is still loading, or dead, doesn't get hungrier. Death takes the statuses off, and after it
/// the meters come back to at least the respawn level, so starving can't become a loop.</para>
/// </summary>
[Service]
public sealed partial class SustenanceService(
    IEntities entities,
    IServerEvents events,
    IPlayers players,
    IStatuses statuses,
    SustenanceStore store,
    FoodTable foods,
    SustenanceConfig config,
    ILogger logger)
{
    private sealed class Meters
    {
        public Guid? Account;
        public double Satiety;
        public double Hydration;
        public float SatietyRate;
        public float HydrationRate;
        public bool WasDead;
        public double NextMealAt;
        public readonly List<AppliedStatus> Statuses = [];
        public StatusKey? StatusesFor;      // the tiers the statuses were last worked out for
        public NaturalVitals Natural;       // the character's maxima, read while no status of ours touches them
    }

    private readonly record struct AppliedStatus(StatusHandle Handle, StatusEffect Effect, Stat? Vital);

    private readonly record struct StatusKey(int Hunger, int Thirst, bool HealthDamage);

    private readonly Dictionary<PlayerId, Meters> _meters = [];
    private readonly Inventories _inventories = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private float _saveElapsed;
    private Tier[] _hungerTiers = [];
    private Tier[] _thirstTiers = [];

    /// <summary>A player's decay rates changed: points per minute for satiety, then hydration.</summary>
    public event Action<PlayerId, float, float>? RatesChanged;

    private double Now => _clock.Elapsed.TotalSeconds;

    private void Start()
    {
        _hungerTiers = SustenanceEffects.Resolve(config.Hunger,
            problem => logger.LogWarning("HungerAndThirst: hunger {Problem} Left out.", problem));
        _thirstTiers = SustenanceEffects.Resolve(config.Thirst,
            problem => logger.LogWarning("HungerAndThirst: thirst {Problem} Left out.", problem));
        events.OnPlayerConnected += OnConnected;
        events.OnPlayerDisconnected += OnDisconnected;
        logger.LogInformation(
            "HungerAndThirst: running. Full to empty in {Starve} min (food) and {Dehydrate} min (drink), effects {Kill}.",
            config.MinutesToStarve, config.MinutesToDehydrate,
            config.HealthFloor > 0 ? $"stop at {config.HealthFloor} health" : "can kill");
    }

    private void Stop()
    {
        events.OnPlayerConnected -= OnConnected;
        events.OnPlayerDisconnected -= OnDisconnected;
        SaveAll();
    }

    private void OnConnected(Player scope, Guid _) => Guard("connect", scope.PlayerId, () => Track(scope.PlayerId));

    private void OnDisconnected(Player scope, Guid _, DisconnectReason reason) => OnDisconnected(scope.PlayerId, reason);

    private void OnDisconnected(PlayerId player, DisconnectReason reason) => Guard("disconnect", player, () =>
    {
        if (!_meters.Remove(player, out Meters? meters))
            return;
        Remember(meters);
        store.Save();
        logger.LogInformation("HungerAndThirst: {Player} left ({Reason}), meters saved.", player, reason);
    });

    /// <summary>Starts keeping meters for a player, from their save or full. Does nothing if already kept.</summary>
    public void Track(PlayerId player)
    {
        if (_meters.ContainsKey(player))
            return;

        Guid? account = players.GetReadyMId(player);
        SavedMeters saved = account is Guid id && store.TryGet(id, out SavedMeters found)
            ? found
            : new SavedMeters(config.StartSatiety, config.StartHydration);
        Meters meters = new()
        {
            Account = account,
            Satiety = SustenanceScale.Clamp(saved.Satiety),
            Hydration = SustenanceScale.Clamp(saved.Hydration),
        };
        _meters[player] = meters;
        Publish(player, meters);
        logger.LogInformation("HungerAndThirst: tracking {Player} at satiety {Satiety}, hydration {Hydration}.",
            player, saved.Satiety, saved.Hydration);
    }

    /// <summary>The player's current decay rates, 0 while loading or dead.</summary>
    public (float Satiety, float Hydration) RatesOf(PlayerId player)
    {
        return _meters.TryGetValue(player, out Meters? meters) ? (meters.SatietyRate, meters.HydrationRate) : (0f, 0f);
    }

    private void Update()
    {
        float delta = Time.DeltaTime;
        foreach ((PlayerId player, Meters meters) in _meters)
        {
            try
            {
                Tick(player, meters, delta);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "HungerAndThirst: tick for {Player} failed.", player);
            }
        }

        _saveElapsed += delta;
        if (_saveElapsed >= config.SaveIntervalMinutes * 60f)
        {
            _saveElapsed = 0f;
            SaveAll();
        }
    }

    private void Tick(PlayerId player, Meters meters, float delta)
    {
        if (!entities.TryLookup<MainCharacter, PlayerId>(player, out MainCharacter character))
        {
            SetRates(player, meters, 0f, 0f); // still loading: no character to get hungry
            return;
        }

        if (character.IsDead)
        {
            meters.WasDead = true;
            SetRates(player, meters, 0f, 0f);
            TakeStatusesOff(player, meters);
            return;
        }
        if (meters.WasDead)
        {
            meters.WasDead = false;
            meters.Satiety = Math.Max(meters.Satiety, SustenanceScale.Clamp(config.RespawnSatiety));
            meters.Hydration = Math.Max(meters.Hydration, SustenanceScale.Clamp(config.RespawnHydration));
        }

        CharacterStateFlags flags = character.CharacterStateFlags;
        float multiplier = SustenanceRules.ActivityMultiplier(
            running: (flags & CharacterStateFlags.IsRunning) != 0,
            swimming: (flags & CharacterStateFlags.IsSwimming) != 0,
            inCombat: character.InCombat,
            config);
        SetRates(player, meters,
            SustenanceRules.PerMinute(config.MinutesToStarve, multiplier),
            SustenanceRules.PerMinute(config.MinutesToDehydrate, multiplier));

        meters.Satiety = SustenanceRules.Decay(meters.Satiety, meters.SatietyRate, delta);
        meters.Hydration = SustenanceRules.Decay(meters.Hydration, meters.HydrationRate, delta);
        Publish(player, meters);
        UpdateStatuses(player, character, meters);
    }

    // Gives the character the statuses of the tiers its meters are in, when a meter moves to another tier or its
    // health reaches the floor. Only the difference is sent: a status that stays the same stays on.
    private void UpdateStatuses(PlayerId player, MainCharacter character, Meters meters)
    {
        meters.Natural = new NaturalVitals(
            NaturalOf(meters, Stat.Health, character.HpBase, meters.Natural.Health),
            NaturalOf(meters, Stat.Magicka, character.MagickaBase, meters.Natural.Magicka),
            NaturalOf(meters, Stat.Fatigue, character.FatigueBase, meters.Natural.Fatigue));
        if (meters.Natural.Health <= 0f || meters.Natural.Fatigue <= 0f)
            return; // the vitals haven't come in yet

        StatusKey key = new(
            SustenanceEffects.TierAt(_hungerTiers, SustenanceRules.Shown(meters.Satiety)),
            SustenanceEffects.TierAt(_thirstTiers, SustenanceRules.Shown(meters.Hydration)),
            config.HealthFloor <= 0 || character.Hp > config.HealthFloor);
        if (meters.StatusesFor == key)
            return;

        // Health as it would be without our own Drain Health, so taking health to the floor doesn't shrink the drain
        // that took it there.
        float drained = meters.Statuses
            .Where(status => status.Effect.EffectCode == DrainHealth)
            .Sum(status => status.Effect.Magnitude);
        List<WantedStatus> missing = SustenanceEffects.Wanted(
            key.Hunger >= 0 ? _hungerTiers[key.Hunger] : null,
            key.Thirst >= 0 ? _thirstTiers[key.Thirst] : null,
            meters.Natural, character.Hp + drained, config.HealthFloor, key.HealthDamage);

        for (int i = meters.Statuses.Count - 1; i >= 0; i--)
        {
            int still = missing.FindIndex(wanted => wanted.Effect == meters.Statuses[i].Effect);
            if (still >= 0)
            {
                missing.RemoveAt(still);
                continue;
            }
            statuses.Remove(player, meters.Statuses[i].Handle);
            meters.Statuses.RemoveAt(i);
        }
        foreach (WantedStatus wanted in missing)
            meters.Statuses.Add(new AppliedStatus(statuses.Apply(player, wanted.Effect), wanted.Effect, wanted.Vital));
        meters.StatusesFor = key;

        logger.LogInformation("HungerAndThirst: {Player} now has {Statuses}.", player,
            meters.Statuses.Count == 0 ? "no effects" : string.Join(", ", meters.Statuses.Select(status => status.Effect)));
    }

    private static readonly uint DrainHealth = StatusEffect.Encode("DRHE");

    // The character's maximum of a vital, read only while none of our statuses acts on it, directly or through an
    // attribute: what the server sees may already include them. Until then the last reading stands.
    private static float NaturalOf(Meters meters, Stat vital, float current, float last)
    {
        bool touched = meters.Statuses.Any(status => status.Vital == vital
            || status.Effect.Stat is { } stat && SustenanceEffects.FeedsInto(stat) == vital);
        return current > 0f && !touched ? current : last;
    }

    private void TakeStatusesOff(PlayerId player, Meters meters)
    {
        foreach (AppliedStatus status in meters.Statuses)
            statuses.Remove(player, status.Handle);
        meters.Statuses.Clear();
        meters.StatusesFor = null;
    }

    /// <summary>
    /// Eats or drinks one <paramref name="formId"/> from the player's inventory: a known food, actually carried, and
    /// not too soon after the last one. Takes the item on the server, then applies it. False with a reason to show
    /// the player otherwise.
    /// </summary>
    public bool TryConsume(PlayerId player, uint formId, out string refusal)
    {
        if (!CanEatNow(player, out Meters? meters, out MainCharacter character, out refusal))
            return false;
        if (!foods.TryGet(formId, out FoodItem food))
        {
            refusal = "You can't eat or drink that.";
            return false;
        }
        if (!TryTakeOne(character, formId))
        {
            refusal = $"You have no {food.Name} left.";
            return false;
        }
        Feed(player, meters, food.Satiety, food.Hydration);
        logger.LogInformation("HungerAndThirst: {Player} had {Food} (+{Satiety} satiety, +{Hydration} hydration).",
            player, food.Name, food.Satiety, food.Hydration);
        return true;
    }

    /// <summary>A sip from the water the player is swimming in.</summary>
    public bool TryDrinkWater(PlayerId player, out string refusal)
    {
        if (!CanEatNow(player, out Meters? meters, out MainCharacter character, out refusal))
            return false;
        if ((character.CharacterStateFlags & CharacterStateFlags.IsSwimming) == 0)
        {
            refusal = "There is no water here to drink.";
            return false;
        }
        Feed(player, meters, 0, config.WaterHydration);
        return true;
    }

    /// <summary>The meters as the client sees them.</summary>
    public bool TryGetShown(PlayerId player, out int satiety, out int hydration)
    {
        satiety = hydration = 0;
        if (!_meters.TryGetValue(player, out Meters? meters))
            return false;
        satiety = SustenanceRules.Shown(meters.Satiety);
        hydration = SustenanceRules.Shown(meters.Hydration);
        return true;
    }

    /// <summary>
    /// Applies a meal that already happened. Every way of eating ends here, so a future "player consumed" event from
    /// the SDK only needs to call this.
    /// </summary>
    public void Feed(PlayerId player, int satiety, int hydration)
    {
        if (_meters.TryGetValue(player, out Meters? meters))
            Feed(player, meters, satiety, hydration);
    }

    private void Feed(PlayerId player, Meters meters, int satiety, int hydration)
    {
        meters.Satiety = SustenanceRules.Gain(meters.Satiety, satiety);
        meters.Hydration = SustenanceRules.Gain(meters.Hydration, hydration);
        meters.NextMealAt = Now + config.ConsumeCooldownSeconds;
        Publish(player, meters);
    }

    private bool CanEatNow(PlayerId player, out Meters meters, out MainCharacter character, out string refusal)
    {
        character = default;
        if (!_meters.TryGetValue(player, out meters!)
            || !entities.TryLookup<MainCharacter, PlayerId>(player, out character))
        {
            refusal = "You are not in the world yet.";
            return false;
        }
        if (character.IsDead)
        {
            refusal = "The dead don't eat.";
            return false;
        }
        if (Now < meters.NextMealAt)
        {
            refusal = "Still chewing.";
            return false;
        }
        refusal = string.Empty;
        return true;
    }

    // Takes one unworn item of this form out of the character's inventory, matching the stack's own condition so
    // the SDK finds exactly that entry.
    private bool TryTakeOne(MainCharacter character, uint formId)
    {
        for (int i = 0; i < character.ItemsCount; i++)
        {
            InventoryEntry entry = character.GetItems(i);
            if (entry.FormId.FormId.Value != formId || entry.Worn != 0 || entry.Count < 1)
                continue;
            ItemStackData stack = new()
            {
                BaseId = entry.FormId,
                Count = 1,
                Durability = entry.Health,
                Charge = entry.Charge,
            };
            return _inventories.TryTake(character.As<Equipment>(), in stack);
        }
        return false;
    }

    private void SetRates(PlayerId player, Meters meters, float satietyRate, float hydrationRate)
    {
        if (Math.Abs(meters.SatietyRate - satietyRate) < 0.01f && Math.Abs(meters.HydrationRate - hydrationRate) < 0.01f)
            return;
        meters.SatietyRate = satietyRate;
        meters.HydrationRate = hydrationRate;
        RatesChanged?.Invoke(player, satietyRate, hydrationRate);
    }

    // Writes the whole-number view onto the player, only when it changed, so a slow meter sends nothing most ticks.
    private void Publish(PlayerId player, Meters meters)
    {
        if (!entities.TryLookup(player, out Player entity))
            return;
        int satiety = SustenanceRules.Shown(meters.Satiety);
        int hydration = SustenanceRules.Shown(meters.Hydration);
        if (entity.Satiety != satiety)
            entity.Satiety = satiety;
        if (entity.Hydration != hydration)
            entity.Hydration = hydration;
    }

    private void Remember(Meters meters)
    {
        if (meters.Account is Guid account)
            store.Put(account, new SavedMeters(SustenanceRules.Shown(meters.Satiety), SustenanceRules.Shown(meters.Hydration)));
    }

    private void SaveAll()
    {
        foreach (Meters meters in _meters.Values)
            Remember(meters);
        store.Save();
    }

    private void Guard(string what, PlayerId player, Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "HungerAndThirst: {What} for {Player} failed.", what, player);
        }
    }
}
