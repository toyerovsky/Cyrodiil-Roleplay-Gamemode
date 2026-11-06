using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OblivionMP.SDK;
using ReadyM.Relay.Common.Oblivion.ECS.Values;
using ReadyM.SDK.Archetypes.Core;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Events;
using Roleplay.HungerAndThirst.Common;

namespace Roleplay.HungerAndThirst.Client;

/// <summary>
/// The client's view of its own hunger and thirst. The server owns the numbers (the replicated
/// <see cref="Sustenance"/> mixin); this only makes them feel immediate:
/// <list type="bullet">
/// <item>Between updates it counts down at the rate the server last sent. The server rounds up and writes a new whole
/// number each time the meter crosses one, so a replicated value v only says the meter is somewhere in (v-1, v]. A
/// countdown already there carries on undisturbed; resetting it to v on every update would make the bar step at the
/// replication rate (about 5 times a second at fast test rates). It may run up to <see cref="PredictionWindow"/>
/// points ahead of the last value, which covers a few late updates.</item>
/// <item>A meal shows at once and is taken out of the satchel at once. The server's <see cref="MealEvent"/> coming
/// back (<c>Stage.Observe</c>) keeps the guess, a refusal (<c>Stage.Rejected</c>) takes it back with the reason.</item>
/// <item>A second meal before the server's cooldown is over is refused here, the way the server would refuse it,
/// so the bar never jumps up for a meal that is about to be rolled back.</item>
/// </list>
/// Also says hello to the server once the player is in the world, which brings the food table and the rates.
/// </summary>
[Service]
public sealed partial class SustenanceState(IPlayers players, SustenanceClientRpc rpc, INetworkedEvents events, ILogger logger)
{
    private const double HelloRetrySeconds = 5.0;
    private const int HelloAttempts = 6;
    private const double MealTimeoutSeconds = 5.0;

    /// <summary>How far, in points, the countdown may run ahead of the last replicated value.</summary>
    private const double PredictionWindow = 5.0;

    // One meal on its way: what was asked for, and what it should give the player (nothing when feeding someone else).
    private sealed record PendingMeal(Player Eater, uint FormId, int Satiety, int Hydration, double SentAt);

    private sealed class Meter
    {
        public int Replicated = -1; // the last value seen on the mixin, -1 before the first
        public double Anchor;       // where the countdown was...
        public double AnchorAt;     // ...and when
        public float Rate;          // points per minute

        public void Set(double value, double now)
        {
            Anchor = value;
            AnchorAt = now;
        }

        // Counted down from the anchor, but never more than the window below the last replicated value.
        public double Predicted(double now)
        {
            double value = Anchor - Rate * (now - AnchorAt) / 60.0;
            double floor = Replicated >= 0 ? Replicated - PredictionWindow : 0;
            return Math.Max(Math.Max(value, floor), 0);
        }
    }

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Meter _satiety = new();
    private readonly Meter _hydration = new();
    private PendingMeal? _asked;   // asked for, not raised yet: a raise belongs on the game's update
    private PendingMeal? _sent;    // raised, no answer yet
    private Dictionary<uint, Food> _foods = [];
    private int _hellos;
    private double _helloAt = double.NegativeInfinity;
    private bool _haveFoods;
    private float _cooldown;
    private int _waterHydration;
    private double _lastMealAt = double.NegativeInfinity;

    /// <summary>Somebody nearby ate or drank, the player included: what the server let through.</summary>
    public event Action<MealNews>? Meal;

    /// <summary>The player's meal or feeding was refused, or got no answer.</summary>
    public event Action<MealRefusal>? MealRefused;

    /// <summary>The server's values have arrived at least once.</summary>
    public bool Ready => _satiety.Replicated >= 0;

    /// <summary>What the HUD shows, 0..<see cref="SustenanceScale.Max"/>, with a meal on its way included.</summary>
    public double Satiety => Shown(_satiety, meal => meal.Satiety);

    public double Hydration => Shown(_hydration, meal => meal.Hydration);

    public float SatietyRate => _satiety.Rate;

    public float HydrationRate => _hydration.Rate;

    /// <summary>The server's food table. Empty until it has been sent.</summary>
    public IReadOnlyCollection<Food> Foods => _foods.Values;

    /// <summary>The character is in water deep enough to swim, so it can drink from it.</summary>
    public bool InWater => players.LocalMainCharacter is { } me
        && (me.CharacterStateFlags & CharacterStateFlags.IsSwimming) != 0;

    private PendingMeal? Pending => _asked ?? _sent;

    private double Now => _clock.Elapsed.TotalSeconds;

    private void Start()
    {
        rpc.FoodsReceived += OnFoods;
        rpc.RatesReceived += OnRates;
    }

    private void Stop()
    {
        rpc.FoodsReceived -= OnFoods;
        rpc.RatesReceived -= OnRates;
    }

    private void Update()
    {
        if (players.LocalPlayer is not { } me)
            return;

        SayHello();
        Observe(_satiety, me.Satiety);
        Observe(_hydration, me.Hydration);

        if (_asked is { } asked)
        {
            _asked = null;
            _sent = asked;
            try
            {
                events.Raise(new MealEvent { Feeder = me, Eater = asked.Eater, FormId = asked.FormId });
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "[HungerAndThirst] Asking the server for a meal failed.");
                Refuse(MealRefusal.Other);
            }
        }
        else if (_sent is { } sent && Now - sent.SentAt > MealTimeoutSeconds)
        {
            logger.LogWarning("[HungerAndThirst] No answer about the meal of {Item:X8}.", sent.FormId);
            Refuse(MealRefusal.Other);
        }
    }

    /// <summary>The foods the player carries, by name. Empty until the server has sent its table.</summary>
    public IReadOnlyList<CarriedFood> Carried()
    {
        if (players.LocalMainCharacter is not { } me || _foods.Count == 0)
            return [];

        Dictionary<uint, int> counts = [];
        for (int i = 0; i < me.ItemsCount; i++)
        {
            InventoryEntry entry = me.GetItems(i);
            uint formId = entry.FormId.FormId.Value;
            if (entry.Worn == 0 && entry.Count > 0 && _foods.ContainsKey(formId))
                counts[formId] = counts.GetValueOrDefault(formId) + entry.Count;
        }
        if (Pending is { FormId: not 0 } meal && counts.ContainsKey(meal.FormId))
            counts[meal.FormId]--;

        return counts
            .Where(pair => pair.Value > 0)
            .Select(pair => new CarriedFood(_foods[pair.Key], pair.Value))
            .OrderBy(carried => carried.Food.Name, StringComparer.CurrentCulture)
            .ToArray();
    }

    /// <summary>Eats or drinks one of these from the player's pack. Shows at once; a refusal takes it back.</summary>
    public void Eat(uint formId)
    {
        if (players.LocalPlayer is not { } me)
            Refuse(MealRefusal.NotInWorld);
        else if (!_foods.TryGetValue(formId, out Food? food))
            Refuse(MealRefusal.NotFood);
        else
            Ask(new PendingMeal(me, formId, food.Satiety, food.Hydration, Now));
    }

    /// <summary>A sip from the water the player is swimming in.</summary>
    public void DrinkWater()
    {
        if (players.LocalPlayer is not { } me)
            Refuse(MealRefusal.NotInWorld);
        else
            Ask(new PendingMeal(me, 0, 0, _waterHydration, Now));
    }

    /// <summary>Gives <paramref name="eater"/> one of these from the player's pack. The server checks they stand near.</summary>
    public void Feed(Player eater, uint formId)
    {
        if (players.LocalPlayer is not { } me)
            Refuse(MealRefusal.NotInWorld);
        else if (eater.PlayerId == me.PlayerId)
            Eat(formId);
        else if (formId == 0)
            Refuse(MealRefusal.WaterForOthers);
        else if (!_foods.ContainsKey(formId))
            Refuse(MealRefusal.NotFood);
        else
            Ask(new PendingMeal(eater, formId, 0, 0, Now));
    }

    // One meal at a time, and not before the server would take the next: refused here, so nothing flickers.
    private void Ask(PendingMeal meal)
    {
        if (Pending is not null || Now - _lastMealAt < _cooldown)
        {
            Refuse(MealRefusal.TooSoon);
            return;
        }
        _lastMealAt = Now;
        _asked = meal;
    }

    /// <summary>Somebody nearby had a meal. The player's own one keeps its guess: the mixin catches up with it.</summary>
    [EventHandler(Stage.Observe)]
    public void OnMeal(in MealEvent meal, EventContext context)
    {
        if (players.LocalPlayer is not { } me)
            return;
        bool byMe = meal.Feeder.PlayerId == me.PlayerId;
        bool toMe = meal.Eater.PlayerId == me.PlayerId;
        if (byMe && _sent is { } sent && sent.FormId == meal.FormId)
        {
            _sent = null;
            double now = Now;
            _satiety.Set(Math.Min(_satiety.Predicted(now) + sent.Satiety, SustenanceScale.Max), now);
            _hydration.Set(Math.Min(_hydration.Predicted(now) + sent.Hydration, SustenanceScale.Max), now);
        }
        Food? food = meal.FormId == 0 ? null : _foods.GetValueOrDefault(meal.FormId);
        MealNews news = new(meal.Feeder.PlayerId, meal.Eater.PlayerId, food, byMe, toMe);
        Raise(() => Meal?.Invoke(news));
    }

    /// <summary>The server, or another mod, refused a meal this client asked for.</summary>
    [EventHandler(Stage.Rejected)]
    public void OnMealRefused(in MealEvent meal, MealRefusal reason, EventContext context)
    {
        if (_sent is not null && context.IsRaisingClient)
            Refuse(reason);
    }

    private void Refuse(MealRefusal reason)
    {
        _asked = null;
        _sent = null;
        Raise(() => MealRefused?.Invoke(reason));
    }

    private double Shown(Meter meter, Func<PendingMeal, int> gain)
    {
        double value = meter.Predicted(Now);
        if (Pending is { } meal)
            value += gain(meal);
        return Math.Clamp(value, 0, SustenanceScale.Max);
    }

    private void Observe(Meter meter, int replicated)
    {
        if (replicated == meter.Replicated)
            return;
        double now = Now;
        double predicted = meter.Predicted(now);
        // Going up only happens for a meal, a feeding or a respawn, and already includes a meal of ours on its way.
        bool up = meter.Replicated >= 0 && replicated > meter.Replicated;
        if (up && _sent is { } sent && sent.Eater.PlayerId == players.LocalPlayer?.PlayerId)
            _sent = null;
        meter.Replicated = replicated;
        // A countdown at or a little ahead of the server keeps going from where it is; one that fell behind, or a
        // meal, moves to the server's number.
        bool inStep = !up && predicted <= replicated && predicted >= replicated - PredictionWindow;
        meter.Set(inStep ? predicted : replicated, now);
    }

    private void SayHello()
    {
        if (_haveFoods || _hellos >= HelloAttempts || Now - _helloAt < HelloRetrySeconds)
            return;
        _hellos++;
        _helloAt = Now;
        rpc.SendHello();
        if (_hellos == HelloAttempts)
            logger.LogWarning("[HungerAndThirst] The server hasn't answered after {Count} hellos, giving up.", _hellos);
    }

    private void OnFoods(IReadOnlyList<Food> foods, float cooldownSeconds, int waterHydration)
    {
        _foods = foods.ToDictionary(food => food.FormId);
        _cooldown = Math.Max(cooldownSeconds, 0f);
        _waterHydration = Math.Max(waterHydration, 0);
        _haveFoods = true;
    }

    private void OnRates(float satietyPerMinute, float hydrationPerMinute)
    {
        // Restart the countdown from where it is now, so a change of pace doesn't make the bar jump.
        double now = Now;
        _satiety.Set(_satiety.Predicted(now), now);
        _hydration.Set(_hydration.Predicted(now), now);
        _satiety.Rate = satietyPerMinute;
        _hydration.Rate = hydrationPerMinute;
    }

    // Listeners are other code, the page and other mods: one that throws must not break the meal bookkeeping.
    private void Raise(Action raise)
    {
        try
        {
            raise();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[HungerAndThirst] A meal listener failed.");
        }
    }
}
