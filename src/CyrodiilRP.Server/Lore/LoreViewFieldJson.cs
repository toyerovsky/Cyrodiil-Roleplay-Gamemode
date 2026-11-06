namespace CyrodiilRP.Server.Lore;

// One field as a reader sees it. Long: a long text, shown in paragraphs.
internal sealed record LoreViewFieldJson(string Id, string Part, string Label, string Text, bool Long);
