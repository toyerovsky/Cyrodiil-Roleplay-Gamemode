using System.Diagnostics;
using System.Numerics;
using LiteNetLib;
using Microsoft.Extensions.Logging;
using OblivionMP.SDK.Common.Archetypes;
using OblivionMP.SDK.Common.Archetypes.Mixins;
using OblivionMP.SDK.Common.Events;
using OblivionMP.SDK.Common.Statuses;
using OblivionMP.SDK.Serverside.Items;
using OblivionMP.SDK.Serverside.Statuses;
using ReadyM.Api.Idents;
using ReadyM.Relay.Common.Oblivion.ECS.Values;
using ReadyM.Relay.Common.Oblivion.Rpc;
using ReadyM.SDK.Server.Events;
using ReadyM.Relay.Server.Sdk.Players;
using ReadyM.SDK.Archetypes.Core;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Events;
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

    /// <summary>
    /// Fills a player's meters to the start values (<c>startSatiety</c>, <c>startHydration</c>), as for a character the
    /// server has never seen: for a mod that gives the player a new character. Their tier effects come off on the
    /// next update.
    /// </summary>
    public void Refill(PlayerId player)
    {
        Track(player);
        Meters meters = _meters[player];
        meters.Satiety = SustenanceScale.Clamp(config.StartSatiety);
        meters.Hydration = SustenanceScale.Clamp(config.StartHydration);
        meters.NextMealAt = 0;
        Publish(player, meters);
        logger.LogInformation("HungerAndThirst: {Player} starts afresh at satiety {Satiety}, hydration {Hydration}.",
            player, config.StartSatiety, config.StartHydration);
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

        // Damage Health comes off at the floor and only goes back on a few points above it, so health hovering at the
        // line (the server sees it a moment late) doesn't switch it every update.
        bool wasDamaging = meters.StatusesFor?.HealthDamage ?? true;
        StatusKey key = new(
            SustenanceEffects.TierAt(_hungerTiers, SustenanceRules.Shown(meters.Satiety)),
            SustenanceEffects.TierAt(_thirstTiers, SustenanceRules.Shown(meters.Hydration)),
            config.HealthFloor <= 0 || character.Hp > config.HealthFloor + (wasDamaging ? 0f : HealthResumeMargin));
        if (meters.StatusesFor == key)
            return;
        bool tiersMoved = meters.StatusesFor is not { } last || last.Hunger != key.Hunger || last.Thirst != key.Thirst;

        // Health as it would be without our own Drain Health, so taking health to the floor doesn't shrink the drain
        // that took it there.
        float drained = meters.Statuses
            .Where(status => status.Effect.EffectCode == DrainHealth)
            .Sum(status => status.Effect.Magnitude);
        List<WantedStatus> missing = SustenanceEffects.Wanted(
            key.Hunger >= 0 ? _hungerTiers[key.Hunger] : null,
            key.Thirst >= 0 ? _thirstTiers[key.Thirst] : null,
            meters.Natural, character.Hp + drained, config.HealthFloor, key.HealthDamage);

        // Health reaching the floor only takes Damage Health off or puts it back. A Drain Health worked out again then
        // would come off and go back on a point smaller, moving health across the floor once more, every update.
        if (!tiersMoved)
        {
            missing.RemoveAll(wanted => wanted.Effect.EffectCode == DrainHealth);
            missing.AddRange(meters.Statuses
                .Where(status => status.Effect.EffectCode == DrainHealth)
                .Select(status => new WantedStatus(status.Effect, status.Vital)));
        }

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

    /// <summary>How far above the health floor Damage Health goes back on, once it came off there.</summary>
    private const float HealthResumeMargin = 5f;

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
    /// Why <paramref name="feeder"/> can't give <paramref name="eater"/> one <paramref name="formId"/> from their pack
    /// right now (0: a sip of the water the feeder swims in, for themselves only), or null when they can. Every
    /// <see cref="MealEvent"/> passes this in <c>Stage.Validate</c>.
    /// </summary>
    public MealRefusal? Check(PlayerId feeder, PlayerId eater, uint formId)
    {
        if (!_meters.TryGetValue(eater, out Meters? meters)
            || !entities.TryLookup<MainCharacter, PlayerId>(feeder, out MainCharacter giver)
            || !entities.TryLookup<MainCharacter, PlayerId>(eater, out MainCharacter taker))
            return MealRefusal.NotInWorld;
        if (giver.IsDead)
            return MealRefusal.FeederDead;
        if (taker.IsDead)
            return MealRefusal.EaterDead;
        if (feeder != eater && !AreNear(giver, taker, config.FeedRange))
            return MealRefusal.TooFar;
        if (formId == 0)
        {
            if (feeder != eater)
                return MealRefusal.WaterForOthers;
            if ((giver.CharacterStateFlags & CharacterStateFlags.IsSwimming) == 0)
                return MealRefusal.NoWater;
        }
        else if (!foods.TryGet(formId, out _))
            return MealRefusal.NotFood;
        else if (CountCarried(giver, formId) < 1)
            return MealRefusal.NotCarried;
        if (Now < meters.NextMealAt)
            return MealRefusal.TooSoon;
        return null;
    }

    [EventHandler(Stage.Validate)]
    public void CheckMeal(in MealEvent meal, EventContext context)
    {
        if (Check(meal.Feeder.PlayerId, meal.Eater.PlayerId, meal.FormId) is { } refusal)
            context.Cancel(refusal);
    }

    /// <summary>A meal that passed every check, this mod's and any other's: takes the item from the feeder's pack
    /// and fills the eater.</summary>
    [EventHandler(Stage.Apply)]
    public void ServeMeal(in MealEvent meal, EventContext context)
    {
        PlayerId feeder = meal.Feeder.PlayerId;
        PlayerId eater = meal.Eater.PlayerId;
        uint formId = meal.FormId;
        Guard("meal", feeder, () => Serve(feeder, eater, formId));
    }

    private void Serve(PlayerId feeder, PlayerId eater, uint formId)
    {
        if (!_meters.TryGetValue(eater, out Meters? meters)
            || !entities.TryLookup<MainCharacter, PlayerId>(feeder, out MainCharacter giver))
        {
            logger.LogError("HungerAndThirst: a checked meal from {Feeder} to {Eater} found nobody to serve.", feeder, eater);
            return;
        }
        if (formId == 0)
        {
            Feed(eater, meters, 0, config.WaterHydration);
            return;
        }
        if (!foods.TryGet(formId, out FoodItem food) || !TryTakeOne(giver, formId))
        {
            logger.LogError("HungerAndThirst: a checked meal of {Item:X8} could not be taken from {Feeder}.", formId, feeder);
            return;
        }
        Feed(eater, meters, food.Satiety, food.Hydration);
        if (feeder == eater)
            logger.LogInformation("HungerAndThirst: {Player} had {Food} (+{Satiety} satiety, +{Hydration} hydration).",
                eater, food.Name, food.Satiety, food.Hydration);
        else
            logger.LogInformation("HungerAndThirst: {Feeder} fed {Eater} {Food} (+{Satiety} satiety, +{Hydration} hydration).",
                feeder, eater, food.Name, food.Satiety, food.Hydration);
    }

    /// <summary>
    /// The player ate or drank from the game's own inventory menu. The game used the item already, so only the meters
    /// move: a food the table knows feeds, anything else (a healing potion) changes nothing. No cooldown, since the
    /// item is gone either way. The player's client raises this, so it is only taken for that player's own character.
    /// </summary>
    [EventHandler(Stage.Apply)]
    public void OnPlayerConsume(in PlayerConsumeEvent consumed, EventContext context)
    {
        if (!config.CountInventoryMeals || context.Origin == PlayerId.Server)
            return;
        if (!consumed.Character.TryAs(out MainCharacter eater) || eater.PlayerId != context.Origin)
        {
            logger.LogWarning("HungerAndThirst: {Player} sent a meal for a character that is not theirs, ignored.",
                context.Origin);
            return;
        }

        PlayerId player = context.Origin;
        uint formId = consumed.Item.FormId.Value;
        Guard("inventory meal", player, () => FeedFromInventory(player, formId));
    }

    private void FeedFromInventory(PlayerId player, uint formId)
    {
        if (!_meters.TryGetValue(player, out Meters? meters) || !foods.TryGet(formId, out FoodItem food))
            return;
        Feed(player, meters, food.Satiety, food.Hydration);
        logger.LogInformation(
            "HungerAndThirst: {Player} had {Food} from the inventory (+{Satiety} satiety, +{Hydration} hydration).",
            player, food.Name, food.Satiety, food.Hydration);
    }

    /// <summary>A player's meters as their client sees them, with the decay rates and the tier each meter is in.</summary>
    public bool TryGetMeters(PlayerId player, out SustenanceMeters view)
    {
        view = default;
        if (!_meters.TryGetValue(player, out Meters? meters))
            return false;
        int satiety = SustenanceRules.Shown(meters.Satiety);
        int hydration = SustenanceRules.Shown(meters.Hydration);
        view = new SustenanceMeters(satiety, hydration, meters.SatietyRate, meters.HydrationRate,
            TierName(_hungerTiers, satiety), TierName(_thirstTiers, hydration));
        return true;
    }

    /// <summary>The foods and drinks of the table a player carries, by name. Empty while they aren't in the world.</summary>
    public IReadOnlyList<CarriedFood> CarriedBy(PlayerId player)
    {
        if (!entities.TryLookup<MainCharacter, PlayerId>(player, out MainCharacter character))
            return [];
        Dictionary<uint, int> counts = [];
        for (int i = 0; i < character.ItemsCount; i++)
        {
            InventoryEntry entry = character.GetItems(i);
            uint formId = entry.FormId.FormId.Value;
            if (entry.Worn == 0 && entry.Count > 0 && foods.TryGet(formId, out _))
                counts[formId] = counts.GetValueOrDefault(formId) + entry.Count;
        }
        return counts
            .Select(pair => foods.TryGet(pair.Key, out FoodItem food) ? new CarriedFood(food, pair.Value) : null)
            .OfType<CarriedFood>()
            .OrderBy(carried => carried.Food.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static string? TierName(Tier[] tiers, int shown)
    {
        int tier = SustenanceEffects.TierAt(tiers, shown);
        return tier >= 0 ? tiers[tier].Name : null;
    }

    /// <summary>Applies a meal that already happened. Every way of eating ends here.</summary>
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

    // Distance only counts within one area: two areas can reuse the same coordinates.
    private bool AreNear(MainCharacter a, MainCharacter b, float range)
    {
        return entities.TryGetScope(a, out Area areaA)
            && entities.TryGetScope(b, out Area areaB)
            && areaA.AreaId == areaB.AreaId
            && Vector3.DistanceSquared(a.Position, b.Position) <= range * range;
    }

    private static int CountCarried(MainCharacter character, uint formId)
    {
        int count = 0;
        for (int i = 0; i < character.ItemsCount; i++)
        {
            InventoryEntry entry = character.GetItems(i);
            if (entry.FormId.FormId.Value == formId && entry.Worn == 0)
                count += entry.Count;
        }
        return count;
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
