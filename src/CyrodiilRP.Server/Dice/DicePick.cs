namespace CyrodiilRP.Server.Dice;

// How many of each die, in the board's order: d6, d10, d20, d100. None at all: the board's tray as it is.
internal sealed record DicePick(int D6, int D10, int D20, int D100)
{
    public static DicePick AsInTray { get; } = new(0, 0, 0, 0);

    public int Total => D6 + D10 + D20 + D100;

    public bool IsValid => Fits(D6) && Fits(D10) && Fits(D20) && Fits(D100) && Total is >= 1 and <= RollNotation.MaxDice;

    public static DicePick From(IEnumerable<DiceGroup> groups)
    {
        var pick = AsInTray;
        foreach (var group in groups)
        {
            pick = group.Kind switch
            {
                DieKind.D6 => pick with { D6 = pick.D6 + group.Count },
                DieKind.D10 => pick with { D10 = pick.D10 + group.Count },
                DieKind.D20 => pick with { D20 = pick.D20 + group.Count },
                DieKind.D100 => pick with { D100 = pick.D100 + group.Count },
                _ => throw new InvalidOperationException($"No die {group.Kind}."),
            };
        }

        return pick;
    }

    // Each count alone first: a sum of huge counts wraps round to a small number.
    private static bool Fits(int count) => count is >= 0 and <= RollNotation.MaxDice;

    // In the board's order, so the values come back as the board lays the dice out.
    public IReadOnlyList<DiceGroup> ToGroups()
    {
        List<DiceGroup> groups = [];
        Add(DieKind.D6, D6);
        Add(DieKind.D10, D10);
        Add(DieKind.D20, D20);
        Add(DieKind.D100, D100);
        return groups;

        void Add(DieKind kind, int count)
        {
            if (count > 0)
                groups.Add(new DiceGroup(kind, count));
        }
    }
}
