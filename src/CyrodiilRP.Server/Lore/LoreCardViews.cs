using System.Globalization;
using System.Text.Json;
using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Players;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Lore;

// A card as one reader sees it, for the lore page, the player's menu and the staff's panel, in the reader's language.
[Service]
internal sealed partial class LoreCardViews(LoreCards cards, Introductions introductions, ShownNames names, RpChat chat, RpConfig config)
{
    // Made at first use, once the config has been read.
    public LoreCardText Text => field ??= new LoreCardText(LoreCatalog.Shipped, config.Lore.CatalogMode == LoreCatalogMode.CanonPlus);

    public LoreCardAnswer BuildCard(OnlinePlayer reader, OnlinePlayer subject)
    {
        cards.Find(subject, out var card);
        var header = BuildHeader(reader, subject, card);
        return new LoreCardAnswer(
            null,
            true,
            subject.Number.ToString(CultureInfo.InvariantCulture),
            chat.Translate(reader, header),
            Relation(reader, subject),
            card.IsEmpty ? null : LoreViews.View(card, FindViewer(reader, subject), header, Text, phrase => chat.Translate(reader, phrase)));
    }

    public string BuildViewPush(OnlinePlayer reader, OnlinePlayer subject) =>
        JsonSerializer.Serialize(BuildCard(reader, subject) with { Op = "view" }, LoreJson.Page.LoreCardAnswer);

    // The whole card for the staff's panel, the notes too.
    public LoreViewJson BuildStaffView(OnlinePlayer staff, OnlinePlayer subject)
    {
        cards.Find(subject, out var card);
        return LoreViews.View(card, LoreViewer.Staff, Text.Header(card, subject.Name, true, LoreViewer.Staff, names.FindLabel(subject)), Text,
            phrase => chat.Translate(staff, phrase));
    }

    // The owner's card as a stranger or an acquaintance reads it.
    public LoreViewJson BuildPreview(OnlinePlayer owner, LoreViewer viewer)
    {
        cards.Find(owner, out var card);
        return LoreViews.View(card, viewer, BuildOwnHeader(owner, card, viewer), Text, phrase => chat.Translate(owner, phrase));
    }

    // How a stranger sees the owner: the first line of their card.
    public string BuildStrangerHeader(OnlinePlayer owner)
    {
        cards.Find(owner, out var card);
        return chat.Translate(owner, BuildOwnHeader(owner, card, LoreViewer.Stranger));
    }

    public Phrase BuildHeader(OnlinePlayer reader, OnlinePlayer subject, LoreCard card) =>
        Text.Header(card, subject.Name, names.KnowsName(reader, subject), FindViewer(reader, subject), names.FindLabel(subject));

    // "known" both ways, "youKnow" or "knowsYou" one way, or "stranger".
    public string Relation(OnlinePlayer reader, OnlinePlayer other)
    {
        var youKnow = introductions.Knows(reader, other);
        var knowsYou = introductions.Knows(other, reader);
        return youKnow && knowsYou ? "known" : youKnow ? "youKnow" : knowsYou ? "knowsYou" : "stranger";
    }

    // The private fields are for those the owner introduced themselves to.
    public LoreViewer FindViewer(OnlinePlayer reader, OnlinePlayer subject) =>
        reader == subject ? LoreViewer.Owner : introductions.Knows(reader, subject) ? LoreViewer.Acquaintance : LoreViewer.Stranger;

    private Phrase BuildOwnHeader(OnlinePlayer owner, LoreCard card, LoreViewer viewer) =>
        Text.Header(card, owner.Name, viewer != LoreViewer.Stranger || card.NamePublic, viewer, names.FindLabel(owner));
}
