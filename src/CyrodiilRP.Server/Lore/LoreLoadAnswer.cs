namespace CyrodiilRP.Server.Lore;

// op load: the editor. Name: the character's own, which the header shows. Header: how a stranger sees the character. Mode: "canon", "canonPlus" or "free".
internal sealed record LoreLoadAnswer(
    bool Ok,
    string Mode,
    bool ReadOnly,
    IReadOnlyList<LoreFieldJson> Fields,
    IReadOnlyDictionary<string, LoreValueJson> Card,
    LoreRaceHintJson? RaceHint,
    int MaxCard,
    int Used,
    string Name,
    string Header,
    bool NamePublic);
