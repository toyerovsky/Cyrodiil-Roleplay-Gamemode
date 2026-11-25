using ReadyM.SDK.Attributes;
using Roleplay.HungerAndThirst.Common;

namespace Roleplay.HungerAndThirst.Server;

/// <summary>The custom items other mods made food or drink (<see cref="ISustenance.RegisterCustomFood"/>), by key.</summary>
[Service]
public sealed partial class CustomFoodRegistry
{
    private readonly Dictionary<string, CustomFood> _foods = new(StringComparer.Ordinal);

    public IReadOnlyCollection<CustomFood> All => _foods.Values;

    public bool TryGet(string key, out CustomFood food) => _foods.TryGetValue(key, out food!);

    /// <summary>Adds a custom food. No key or name, values outside the scale or none above 0, or a key taken, throw.</summary>
    public void Register(CustomFood food)
    {
        if (string.IsNullOrWhiteSpace(food.Key) || string.IsNullOrWhiteSpace(food.Name))
            throw new ArgumentException("A custom food needs the key of its custom item and a name.", nameof(food));
        if (food.Satiety is < 0 or > SustenanceScale.Max || food.Hydration is < 0 or > SustenanceScale.Max
            || food.Satiety + food.Hydration == 0)
            throw new ArgumentException(
                $"{food.Key} needs satiety and hydration from 0 to {SustenanceScale.Max}, and at least one above 0.", nameof(food));
        if (!_foods.TryAdd(food.Key, food))
            throw new InvalidOperationException($"{food.Key} is a custom food already.");
    }
}
