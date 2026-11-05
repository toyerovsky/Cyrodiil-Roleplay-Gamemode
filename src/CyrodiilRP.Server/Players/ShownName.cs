using CyrodiilRP.Server.Localization;

namespace CyrodiilRP.Server.Players;

// A character's name in a text, as each reader sees it: the name for those who know it, the race for strangers. The chat
// resolves it per reader before the text is translated (ShownNames.Resolve).
internal sealed record ShownName(OnlinePlayer Subject)
{
    public static bool IsIn(Phrase text) =>
        text.Args.Any(arg => arg.Value is ShownName || (arg.Value is Phrase inner && IsIn(inner)));

    // The same text with every shown name written as one reader sees it, in the phrases inside it too.
    public static Phrase Resolve(Phrase text, Func<OnlinePlayer, string> shown) =>
        IsIn(text) ? new Phrase(text.Key, text.Args.Select(arg => Resolve(arg, shown)).ToArray()) : text;

    private static TextArg Resolve(TextArg arg, Func<OnlinePlayer, string> shown) => arg.Value switch
    {
        ShownName name => arg with { Value = shown(name.Subject) },
        Phrase inner => arg with { Value = Resolve(inner, shown) },
        _ => arg,
    };
}
