namespace CyrodiilRP.Server.Fishing;

// A fishing rod as an item: its grade in one condition.
internal sealed record RodItem(RodKind Rod, RodCondition Condition)
{
    private const double HalfThePrice = 0.5;

    public string Key => Condition switch
    {
        RodCondition.Good => Rod.Key,
        RodCondition.Worn => Rod.Key + ".worn",
        RodCondition.Damaged => Rod.Key + ".damaged",
        RodCondition.Broken => Rod.Key + ".broken",
    };

    public string Name => Condition switch
    {
        RodCondition.Good => Rod.Name,
        RodCondition.Worn => Rod.Name + " (Worn)",
        RodCondition.Damaged => Rod.Name + " (Damaged)",
        RodCondition.Broken => Rod.Name + " (Broken)",
    };

    public bool Usable => Condition != RodCondition.Broken;

    // The strain its line takes: worn line holds a little less.
    public double Line => Rod.Line * Condition switch
    {
        RodCondition.Good => 1.0,
        RodCondition.Worn => 0.95,
        RodCondition.Damaged => 0.85,
        RodCondition.Broken => 0.0,
    };

    // What the game values it at: half the price when good, less as it wears.
    public int Value => Math.Max(1, (int)Math.Round(Rod.Price * HalfThePrice * Condition switch
    {
        RodCondition.Good => 1.0,
        RodCondition.Worn => 0.8,
        RodCondition.Damaged => 0.6,
        RodCondition.Broken => 0.2,
    }));

    // The condition a step of wear leaves; a broken rod wears no further.
    public RodItem? WornDown => Condition switch
    {
        RodCondition.Good => this with { Condition = RodCondition.Worn },
        RodCondition.Worn => this with { Condition = RodCondition.Damaged },
        RodCondition.Damaged => this with { Condition = RodCondition.Broken },
        RodCondition.Broken => null,
    };

    public static int StepsFromGood(RodCondition condition) => condition switch
    {
        RodCondition.Good => 0,
        RodCondition.Worn => 1,
        RodCondition.Damaged => 2,
        RodCondition.Broken => 3,
    };
}
