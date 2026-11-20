namespace CyrodiilRP.Server.Crafting;

// classification.json: the rules in order, the first that fits an item classes it, and the level tables they name.
internal sealed record ClassificationFile(
    Dictionary<string, Dictionary<string, int>> Tables,
    List<ClassificationRuleEntry> Rules);
