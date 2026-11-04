using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OblivionMp.Sdk;
using ReadyM.Relay.Common.Oblivion.ECS.Values;
using ReadyM.SDK.Attributes;
using Roleplay.HungerAndThirst.Common;

namespace Roleplay.HungerAndThirst.Client;

/// <summary>A food the player carries: how many, minus the ones already on their way to the server.</summary>
public sealed record CarriedFood(Food Food, int Count);

/// <summary>
/// The client's view of its own hunger and thirst. The server owns the numbers (the replicated
/// <see cref="Sustenance"/> mixin); this only makes them feel immediate:
/// <list type="bullet">
/// <item>Between updates it counts down at the rate the server last sent. The server rounds up and writes a new whole
/// number each time the meter crosses one, so a replicated value v only says the meter is somewhere in (v-1, v]. A
/// countdown already there carries on undisturbed; resetting it to v on every update would make the bar step at the
/// replication rate (about 5 times a second at fast test rates). It may run up to <see cref="PredictionWindow"/>
/// points ahead of the last value, which covers a few late updates.</item>
/// <item>A meal shows at once and is taken out of the satchel at once. The server's answer replaces the guess, or
/// rolls it back with a reason.</item>
/// <item>A second meal before the server's cooldown is over is refused here, the way the server would refuse it,
/// so the bar never jumps up for a meal that is about to be rolled back.</item>
/// </list>
/// Also says hello to the server once the player is in the world, which brings the food table and the rates.
/// </summary>
[Service]
public sealed partial class SustenanceState(IPlayers players, SustenanceClientRpc rpc, ILogger logger)
{
    private const double HelloRetrySeconds = 5.0;
    private const int HelloAttempts = 6;
    private const double MealTimeoutSeconds = 5.0;

    /// <summary>How far, in points, the countdown may run ahead of the last replicated value.</summary>
    private const double PredictionWindow = 5.0;

    private readonly record struct Meal(uint FormId, string Name, int Satiety, int Hydration, double SentAt);

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
    private readonly Dictionary<long, Meal> _meals = [];
    private readonly List<long> _expired = [];
    private Dictionary<uint, Food> _foods = [];
    private long _nextRequestId = 1;
    private int _hellos;
    private double _helloAt = double.NegativeInfinity;
    private bool _haveFoods;
    private float _cooldown;
    private double _lastMealAt = double.NegativeInfinity;

    /// <summary>A meal went through: what it was.</summary>
    public event Action<string>? MealEaten;

    /// <summary>A meal was refused or got no answer: the reason to show.</summary>
    public event Action<string>? MealRefused;

    /// <summary>The server's values have arrived at least once.</summary>
    public bool Ready => _satiety.Replicated >= 0;

    /// <summary>What the HUD shows, 0..<see cref="SustenanceScale.Max"/>, with meals on their way included.</summary>
    public double Satiety => Shown(_satiety, meal => meal.Satiety);

    public double Hydration => Shown(_hydration, meal => meal.Hydration);

    public float SatietyRate => _satiety.Rate;

    public float HydrationRate => _hydration.Rate;

    /// <summary>The character is in water deep enough to swim, so it can drink from it.</summary>
    public bool InWater => players.LocalMainCharacter is { } me
        && (me.CharacterStateFlags & CharacterStateFlags.IsSwimming) != 0;

    private double Now => _clock.Elapsed.TotalSeconds;

    private void Start()
    {
        rpc.FoodsReceived += OnFoods;
        rpc.RatesReceived += OnRates;
        rpc.Consumed += OnConsumed;
        rpc.Refused += OnRefused;
    }

    private void Stop()
    {
        rpc.FoodsReceived -= OnFoods;
        rpc.RatesReceived -= OnRates;
        rpc.Consumed -= OnConsumed;
        rpc.Refused -= OnRefused;
    }

    private void Update()
    {
        if (players.LocalPlayer is not { } me)
            return;

        SayHello();
        Observe(_satiety, me.Satiety);
        Observe(_hydration, me.Hydration);

        foreach ((long id, Meal meal) in _meals)
            if (Now - meal.SentAt > MealTimeoutSeconds)
                _expired.Add(id);
        foreach (long id in _expired)
        {
            _meals.Remove(id);
            MealRefused?.Invoke("No answer from the server. Try again.");
        }
        _expired.Clear();
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
        foreach (Meal meal in _meals.Values)
            if (meal.FormId != 0 && counts.ContainsKey(meal.FormId))
                counts[meal.FormId]--;

        return counts
            .Where(pair => pair.Value > 0)
            .Select(pair => new CarriedFood(_foods[pair.Key], pair.Value))
            .OrderBy(carried => carried.Food.Name, StringComparer.CurrentCulture)
            .ToArray();
    }

    /// <summary>Asks the server to eat or drink one of these. Shows the result at once.</summary>
    public void Use(uint formId)
    {
        if (!_foods.TryGetValue(formId, out Food? food))
            return;
        Send(new Meal(formId, food.Name, food.Satiety, food.Hydration, Now), id => rpc.SendConsume(id, formId));
    }

    /// <summary>Asks the server for a sip from the water the player is in. The server knows how much it gives;
    /// until it answers, the guess is nothing.</summary>
    public void DrinkWater()
    {
        Send(new Meal(0, "water", 0, 0, Now), id => rpc.SendDrinkWater(id));
    }

    private void Send(Meal meal, Action<long> send)
    {
        // One meal at a time, and not before the server would take the next: refused here, so nothing flickers.
        if (_meals.Count > 0 || Now - _lastMealAt < _cooldown)
        {
            MealRefused?.Invoke("Still chewing.");
            return;
        }
        _lastMealAt = Now;
        long id = _nextRequestId++;
        _meals[id] = meal;
        send(id);
    }

    private double Shown(Meter meter, Func<Meal, int> gain)
    {
        double value = meter.Predicted(Now);
        foreach (Meal meal in _meals.Values)
            value += gain(meal);
        return Math.Clamp(value, 0, SustenanceScale.Max);
    }

    private void Observe(Meter meter, int replicated)
    {
        if (replicated == meter.Replicated)
            return;
        double now = Now;
        double predicted = meter.Predicted(now);
        // Going up only happens for a meal or a respawn, which already includes any meal still on its way.
        bool up = meter.Replicated >= 0 && replicated > meter.Replicated;
        if (up)
            _meals.Clear();
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

    private void OnFoods(IReadOnlyList<Food> foods, float cooldownSeconds)
    {
        _foods = foods.ToDictionary(food => food.FormId);
        _cooldown = Math.Max(cooldownSeconds, 0f);
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

    private void OnConsumed(long requestId, int satiety, int hydration)
    {
        if (!_meals.Remove(requestId, out Meal meal))
            return;
        // The answer can beat the mixin here; the mixin catches up with the same values a moment later.
        double now = Now;
        _satiety.Set(satiety, now);
        _hydration.Set(hydration, now);
        MealEaten?.Invoke(meal.Name);
    }

    private void OnRefused(long requestId, string reason)
    {
        if (_meals.Remove(requestId))
            MealRefused?.Invoke(reason);
    }
}
