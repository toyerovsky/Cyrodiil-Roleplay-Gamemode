namespace CyrodiilRP.Server.Crafting;

// A rule of classification.json. Type: the item's record ("WEAP"). Id: a regular expression over its editor id. A
// product names its trade and either a level or a table, which gives the level by the id's group "key".
internal sealed record ClassificationRuleEntry(
    ItemClass Class,
    string? Type = null,
    string? Id = null,
    Trade? Trade = null,
    int? Level = null,
    string? Table = null,
    bool Pattern = false);
