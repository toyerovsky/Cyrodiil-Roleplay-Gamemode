namespace CyrodiilRP.Server.Lore;

// op view, and the server's own push of a card (Op "view", request id 0). Who: the header as the reader sees it. View:
// null for an empty card.
internal sealed record LoreCardAnswer(string? Op, bool Ok, string Id, string Who, string Relation, LoreViewJson? View);
