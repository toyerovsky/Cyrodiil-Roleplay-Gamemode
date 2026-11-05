namespace CyrodiilRP.Server.Dice;

// Only a look: any player may take any set, and a set changes nothing of a roll. The page draws them (ui/dice).
internal static class DiceSets
{
    public const string Default = "imperial";

    public static IReadOnlyList<string> All { get; } =
    [
        "altmer", "argonian", "ayleid", "bosmer", "breton", "daedric",
        "dunmer", "imperial", "khajiit", "nord", "orc", "redguard",
    ];

    public static bool IsKnown(string? set) => set is not null && All.Contains(set, StringComparer.Ordinal);
}
