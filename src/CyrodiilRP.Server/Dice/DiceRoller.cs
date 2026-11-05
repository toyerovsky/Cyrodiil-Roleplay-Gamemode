using System.Security.Cryptography;

namespace CyrodiilRP.Server.Dice;

internal static class DiceRoller
{
    private const int D100Faces = 10;
    private const int D100Step = 10;

    public static RollResult Roll(IReadOnlyList<DiceGroup> groups, string? reason, DateTimeOffset at, Func<DieKind, int> rollDie)
    {
        List<RolledDie> dice = [];
        foreach (var group in groups)
        {
            for (var i = 0; i < group.Count; i++)
                dice.Add(new RolledDie(group.Kind, rollDie(group.Kind)));
        }

        return new RollResult(groups, dice, dice.Sum(die => die.Value), reason, at);
    }

    // Cryptographic: even, and nothing in earlier rolls tells the next.
    public static int RollDie(DieKind kind) => kind == DieKind.D100
        ? D100Step * RandomNumberGenerator.GetInt32(1, D100Faces + 1)
        : RandomNumberGenerator.GetInt32(1, (int)kind + 1);
}
