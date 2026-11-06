namespace CyrodiilRP.Server.Lore;

// A field as the editor builds its tabs and inputs. Part and Type in lower camel case (LorePart, LoreFieldType),
// Vis: "public" or "private". Kinds, RaceScoped and Historical serve only the page's stand-in server in a browser.
internal sealed record LoreFieldJson(
    string Id,
    string Part,
    string Label,
    string Type,
    int Max,
    string Vis,
    bool Locked,
    bool OwnerOnly,
    string Hint,
    IReadOnlyList<LoreChoiceJson>? Choices,
    string? Needs,
    string? Group,
    IReadOnlyList<string>? Kinds,
    bool RaceScoped,
    bool Historical);
