using System.Globalization;
using System.Text.Json;
using CyrodiilRP.Data;
using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Dice;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Players;
using Microsoft.Extensions.Logging;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Lore;

// The lore card page's requests, a JSON text in and one out, as MESOL's page asks them. Every change goes through
// LoreEditor, and the page only ever edits its own player's card. Every text comes in the reader's language.
[Service]
internal sealed partial class LoreDesk(
    LoreCards cards,
    LoreCardViews views,
    CharacterRaces races,
    LoreIntroductions handshakes,
    LoreTargets targets,
    Rolls dice,
    DiceBoards boards,
    CommandRegistry commands,
    OnlinePlayers online,
    RpChat chat,
    RpConfig config,
    SystemClock clock,
    ILogger logger)
{
    // Opening the card and saving a few fields takes a handful.
    public const int RequestLimit = 40;

    public static readonly TimeSpan RequestWindow = TimeSpan.FromSeconds(10);

    public const string Introduce = "introduce";

    private LoreEditor? _editor;

    // Made at first use, once the config has been read.
    public LoreEditor Editor => _editor ??= new LoreEditor(LoreCatalog.Shipped, config.Lore.CatalogMode, config.Lore.AllowImplausibleSubtypes);

    private LoreCardText Text => views.Text;

    public string Handle(OnlinePlayer sender, string json)
    {
        if (!Admit(sender))
            return Message(sender, false, LoreText.TooFast);

        LoreOp op;
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
            op = ReadOp(Read(document.RootElement, "op"));
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "[CyrodiilRP] {Player}'s lore card sent a request that is no JSON.", sender.Id);
            return Message(sender, false, LoreText.BadRequest);
        }

        using (document)
        {
            if (op == LoreOp.Unrecognized)
            {
                logger.LogWarning("[CyrodiilRP] {Player}'s lore card asked for {Op}, which is no request.", sender.Id, Read(document.RootElement, "op"));
                return Message(sender, false, LoreText.BadRequest);
            }

            try
            {
                return Run(sender, op, document.RootElement);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "[CyrodiilRP] The lore card request {Op} of {Player} failed.", op, sender.Id);
                return Message(sender, false, LoreText.Failed);
            }
        }
    }

    public string Refuse(OnlinePlayer reader, Phrase why) => Message(reader, false, why);

    public static string BuildEditPush() => JsonSerializer.Serialize(new LorePush("edit"), LoreJson.Page.LorePush);

    private string Run(OnlinePlayer sender, LoreOp op, JsonElement request)
    {
        switch (op)
        {
            case LoreOp.Nearby:
                return Serialize(BuildNearby(sender));

            case LoreOp.View:
                return FindPlayer(request) is { } subject
                    ? Serialize(views.BuildCard(sender, subject))
                    : Message(sender, false, LoreText.PlayerGone);

            case LoreOp.Introduce:
            case LoreOp.Accept:
            case LoreOp.Decline:
            case LoreOp.Forget:
                return RunSocial(sender, op, request);
        }

        var state = cards.Find(sender, out var card);
        switch (state)
        {
            case LoreCardState.NoCharacter:
                return Message(sender, false, ChatText.NeedCharacter);

            case LoreCardState.Loading:
                return Message(sender, false, LoreText.Loading);

            case LoreCardState.Unreadable when op == LoreOp.Load:
                return Serialize(BuildLoad(sender, card, readOnly: true));

            case LoreCardState.Unreadable:
                return Message(sender, false, LoreText.Unreadable);

            case LoreCardState.Ready:
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(op), state, "No such card state.");
        }

        if (Editor.DropCustom(card) is { } dropped)
        {
            cards.Save(sender, dropped);
            card = dropped.Card;
            logger.LogInformation("[CyrodiilRP] {Player}'s lore card lost words typed into fields of the lore list (the server is canon).", sender.Id);
        }

        switch (op)
        {
            case LoreOp.Load:
                return Serialize(BuildLoad(sender, card, readOnly: false));

            case LoreOp.Preview:
                return Read(request, "level") switch
                {
                    "public" => Serialize(new LorePreviewAnswer(true, views.BuildPreview(sender, LoreViewer.Stranger))),
                    "private" => Serialize(new LorePreviewAnswer(true, views.BuildPreview(sender, LoreViewer.Acquaintance))),
                    _ => Message(sender, false, LoreText.PreviewLevel),
                };

            case LoreOp.NameVisibility:
                return SetNameVisibility(sender, card, Read(request, "vis"));
        }

        if (!LoreFields.TryGet(Read(request, "field"), out var field))
            return Message(sender, false, LoreText.NoField(Shorten(Read(request, "field"))));

        if (op == LoreOp.Options)
            return Serialize(BuildOptions(sender, card, field));

        LoreRollAnswerJson? roll = null;
        var edit = op switch
        {
            LoreOp.Set => field.Type == LoreFieldType.Catalog && Editor.Mode == LoreCatalogMode.Canon
                ? LoreEdit.Refused(card, LoreText.PickedOnly(field.Label))
                : Editor.Set(card, field, Read(request, "text")),
            LoreOp.Pick => LoreCatalog.Shipped.TryGet(Read(request, "id"), out var entry)
                ? Editor.Pick(card, field, entry)
                : LoreEdit.Refused(card, LoreText.NotInList),
            LoreOp.Visibility => ReadVisibility(Read(request, "vis")) is { } visibility
                ? Editor.SetVisibility(card, field, visibility)
                : LoreEdit.Refused(card, LoreText.WhichVisibility),
            LoreOp.Clear => Editor.Clear(card, field),
            LoreOp.Roll => Roll(sender, card, field, out roll),
            LoreOp.Keep => Editor.Keep(card, field),
            _ => throw new ArgumentOutOfRangeException(nameof(op), op, "Not an edit of a field."),
        };
        if (edit.Ok)
        {
            cards.Save(sender, edit);
            logger.LogDebug("[CyrodiilRP] {Player} lore card {Op} {Field}.", sender.Id, op, field.Id);
        }

        return Serialize(BuildEdited(sender, edit, roll));
    }

    private LoreLoadAnswer BuildLoad(OnlinePlayer sender, LoreCard card, bool readOnly)
    {
        string Translate(Phrase phrase) => chat.Translate(sender, phrase);
        LoreRaceHintJson? hint = null;
        if (card.Get(LoreFields.Get(LoreFields.Race))?.CatalogId is null
            && GameRaces.FindCatalogId(races.Find(sender.Id)) is { } raceId
            && LoreCatalog.Shipped.TryGet(raceId, out var race))
            hint = new LoreRaceHintJson(race.Id, race.Name);

        return new LoreLoadAnswer(true, LoreViews.Word(Editor.Mode), readOnly, LoreViews.Fields(Translate),
            LoreViews.Values(card, Text, LoreCatalog.Shipped, Translate), hint, LoreFields.MaxCardCharacters, card.TextLength, sender.Name,
            views.BuildStrangerHeader(sender), card.NamePublic);
    }

    private LoreEditAnswer BuildEdited(OnlinePlayer sender, LoreEdit edit, LoreRollAnswerJson? roll) =>
        new(edit.Ok, chat.Translate(sender, edit.Message),
            LoreViews.Values(edit.Card, Text, LoreCatalog.Shipped, phrase => chat.Translate(sender, phrase)), edit.Card.TextLength, roll,
            edit.Card.NamePublic);

    // Homeland: the race's own province first, then the rest of Tamriel.
    private LoreOptionsAnswer BuildOptions(OnlinePlayer sender, LoreCard card, LoreFieldDefinition field)
    {
        var found = Editor.Candidates(card, field, string.Empty, LoreEditor.MaxOptions, out var problem);
        var native = field.Id == LoreFields.Homeland ? LoreEditor.FindNativeProvince(Editor.FindRace(card)) : null;
        var nativeGroup = chat.Translate(sender, LoreText.NativeLands);
        var otherGroup = chat.Translate(sender, LoreText.OtherProvinces);
        var options = found
            .OrderBy(entry => native is not null && entry.Id == native ? 0 : 1)
            .Select(entry => LoreViews.Option(entry, native is null ? null : entry.Id == native ? nativeGroup : otherGroup))
            .ToList();
        return new LoreOptionsAnswer(true, field.Id, options, problem is null ? null : chat.Translate(sender, problem));
    }

    // The trait's 5d20 is a real roll: those around read it in the chat, and the dice land on the small board.
    private LoreEdit Roll(OnlinePlayer sender, LoreCard card, LoreFieldDefinition field, out LoreRollAnswerJson? roll)
    {
        roll = null;
        if (Editor.CheckRoll(card, field, out var kind) is { } refused)
            return LoreEdit.Refused(card, refused);

        if (commands.FindSpeechRefusal(sender) is { } muted)
            return LoreEdit.Refused(card, muted);

        IReadOnlyList<DiceGroup> groups = [new DiceGroup(DieKind.D20, LoreFields.TraitDice)];
        var result = dice.Roll(sender, groups, field.Label);
        boards.Present(sender, DicePick.From(groups), result, field.Label);
        var values = result.Dice.Select(die => die.Value).ToList();
        var edit = Editor.ApplyRoll(card, field, values);
        if (edit.Ok)
            roll = new LoreRollAnswerJson(values, result.Total, kind == LoreRollKind.Reroll);

        return edit;
    }

    private string SetNameVisibility(OnlinePlayer sender, LoreCard card, string word)
    {
        if (ReadVisibility(word) is not { } visibility)
            return Serialize(BuildEdited(sender, LoreEdit.Refused(card, LoreText.WhichVisibility), null));

        var namePublic = visibility == LoreVisibility.Public;
        cards.SetNamePublic(sender, namePublic);
        var named = card with { NamePublic = namePublic };
        return Serialize(BuildEdited(sender, new LoreEdit(true, namePublic ? LoreText.NamePublic : LoreText.NamePrivate, named, []), null));
    }

    private string RunSocial(OnlinePlayer sender, LoreOp op, JsonElement request)
    {
        if (op == LoreOp.Decline && Read(request, "player").Length == 0)
            return Message(sender, handshakes.DeclineFrom(sender, null));

        if (FindPlayer(request) is not { } other)
            return Message(sender, false, LoreText.PlayerGone);

        switch (op)
        {
            case LoreOp.Introduce:
                return commands.FindSpeechRefusal(sender) is { } muted
                    ? Message(sender, false, muted)
                    : Message(sender, handshakes.Offer(sender, other));

            case LoreOp.Accept:
                return Message(sender, handshakes.AcceptFrom(sender, other));

            case LoreOp.Decline:
                return Message(sender, handshakes.DeclineFrom(sender, other));

            case LoreOp.Forget:
                return Message(sender, handshakes.Forget(sender, other));

            default:
                throw new ArgumentOutOfRangeException(nameof(op), op, "Not a request about another player.");
        }
    }

    private LoreNearbyAnswer BuildNearby(OnlinePlayer reader)
    {
        var players = targets.ListNearby(reader)
            .Select(nearby =>
            {
                var other = nearby.Player;
                cards.Find(other, out var card);
                return new LoreNearbyPlayerJson(
                    other.Number.ToString(CultureInfo.InvariantCulture),
                    chat.Translate(reader, views.BuildHeader(reader, other, card)),
                    Math.Round(nearby.Metres, 1),
                    views.Relation(reader, other),
                    !card.IsEmpty,
                    handshakes.IsWaiting(other, reader) ? Introduce : null,
                    handshakes.IsWaiting(reader, other) ? Introduce : null);
            })
            .ToList();
        return new LoreNearbyAnswer(true, players, LoreTargets.RangeMetres);
    }

    private OnlinePlayer? FindPlayer(JsonElement request) =>
        ushort.TryParse(Read(request, "player"), NumberStyles.None, CultureInfo.InvariantCulture, out var number)
        && online.TryFind(number, out var player) && player.IsLoaded && player.Character is not null
            ? player
            : null;

    private bool Admit(OnlinePlayer sender)
    {
        var now = clock.MonotonicMs;
        var oldest = now - (long)RequestWindow.TotalMilliseconds;
        while (sender.RecentLoreAsks.TryPeek(out var at) && at <= oldest)
            sender.RecentLoreAsks.Dequeue();

        if (sender.RecentLoreAsks.Count >= RequestLimit)
            return false;

        sender.RecentLoreAsks.Enqueue(now);
        return true;
    }

    private string Message(OnlinePlayer reader, LoreOutcome outcome) => Message(reader, outcome.Ok, outcome.Message);

    private string Message(OnlinePlayer reader, bool ok, Phrase message) =>
        JsonSerializer.Serialize(new LoreMessageAnswer(ok, chat.Translate(reader, message)), LoreJson.Page.LoreMessageAnswer);

    private static LoreVisibility? ReadVisibility(string word) => word switch
    {
        "public" => LoreVisibility.Public,
        "private" => LoreVisibility.Private,
        _ => null,
    };

    private static LoreOp ReadOp(string word) => word switch
    {
        "load" => LoreOp.Load,
        "options" => LoreOp.Options,
        "set" => LoreOp.Set,
        "pick" => LoreOp.Pick,
        "vis" => LoreOp.Visibility,
        "clear" => LoreOp.Clear,
        "roll" => LoreOp.Roll,
        "keep" => LoreOp.Keep,
        "preview" => LoreOp.Preview,
        "namevis" => LoreOp.NameVisibility,
        "nearby" => LoreOp.Nearby,
        "view" => LoreOp.View,
        "introduce" => LoreOp.Introduce,
        "accept" => LoreOp.Accept,
        "decline" => LoreOp.Decline,
        "forget" => LoreOp.Forget,
        _ => LoreOp.Unrecognized,
    };

    // A field the page names goes back into a message: never more than an id is long.
    private static string Shorten(string text) => text.Length <= LoreFields.MaxIdLength ? text : text[..LoreFields.MaxIdLength];

    private static string Read(JsonElement request, string name) =>
        request.ValueKind == JsonValueKind.Object && request.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static string Serialize(LoreLoadAnswer answer) => JsonSerializer.Serialize(answer, LoreJson.Page.LoreLoadAnswer);

    private static string Serialize(LoreEditAnswer answer) => JsonSerializer.Serialize(answer, LoreJson.Page.LoreEditAnswer);

    private static string Serialize(LoreOptionsAnswer answer) => JsonSerializer.Serialize(answer, LoreJson.Page.LoreOptionsAnswer);

    private static string Serialize(LorePreviewAnswer answer) => JsonSerializer.Serialize(answer, LoreJson.Page.LorePreviewAnswer);

    private static string Serialize(LoreNearbyAnswer answer) => JsonSerializer.Serialize(answer, LoreJson.Page.LoreNearbyAnswer);

    private static string Serialize(LoreCardAnswer answer) => JsonSerializer.Serialize(answer, LoreJson.Page.LoreCardAnswer);
}
