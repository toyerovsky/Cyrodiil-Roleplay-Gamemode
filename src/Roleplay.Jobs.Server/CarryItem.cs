namespace Roleplay.Jobs.Server;

/// <summary>A <see cref="CarryKind"/> with what it takes from its category filled in: everything carrying needs.</summary>
public sealed record CarryItem(string Name, string Asset, float Scale, string Category, int Weight, int DrainSpeed, CarryFit Standing,
    CarryFit Moving)
{
    /// <summary>
    /// Every kind in <paramref name="config"/>, in its order, its weight, Speed drain and fits its own where it gives
    /// them, else its category's. The moving fit is the kind's, else the category's, else the standing fit.
    /// </summary>
    /// <exception cref="InvalidOperationException">A kind names a category the config does not have, or two kinds or two
    /// categories share a name.</exception>
    public static IReadOnlyList<CarryItem> AllOf(CarryConfig config)
    {
        RequireUnique(config.Categories.Select(category => category.Name), "categories");
        RequireUnique(config.Kinds.Select(kind => kind.Name), "kinds");
        var categories = config.Categories.ToDictionary(category => category.Name);

        return config.Kinds.Select(kind =>
        {
            if (!categories.TryGetValue(kind.Category, out var category))
                throw new InvalidOperationException(
                    $"Jobs config: the carry kind '{kind.Name}' is of category '{kind.Category}', which Carrying.Categories does not list.");
            var standing = kind.Standing ?? category.Standing;
            return new CarryItem(kind.Name, kind.Asset, kind.Scale, category.Name, kind.Weight ?? category.Weight,
                kind.DrainSpeed ?? category.DrainSpeed, standing, kind.Moving ?? category.Moving ?? standing);
        }).ToList();
    }

    private static void RequireUnique(IEnumerable<string> names, string what)
    {
        if (names.GroupBy(name => name).FirstOrDefault(group => group.Count() > 1) is { } twice)
            throw new InvalidOperationException($"Jobs config: Carrying.{what} has '{twice.Key}' more than once.");
    }
}
