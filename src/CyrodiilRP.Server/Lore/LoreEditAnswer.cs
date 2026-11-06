namespace CyrodiilRP.Server.Lore;

// op set, pick, vis, clear, roll, keep and namevis: the card after it, and what the player reads about it.
internal sealed record LoreEditAnswer(
    bool Ok,
    string Message,
    IReadOnlyDictionary<string, LoreValueJson> Card,
    int Used,
    LoreRollAnswerJson? Roll,
    bool NamePublic);
