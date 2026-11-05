using System.Collections.Immutable;
using CyrodiilRP.Data;
using CyrodiilRP.Data.Lore;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Players;
using Microsoft.Extensions.Logging;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Lore;

// The lore cards of the characters in play, read as a player loads. Writes go through the one writer, in order.
[Service]
internal sealed partial class LoreCards(
    Persistence persistence,
    Fire fire,
    OnlinePlayers online,
    Callbacks callbacks,
    SystemClock clock,
    ILogger logger)
{
    private const string LoadJob = "lore.load";
    private const string SaveJob = "lore.save";
    private const string NameJob = "lore.name";

    private readonly LoreCardRegistry _cards = new();

    public event Action<OnlinePlayer>? NamePublicChanged;

    public LoreCardState Find(OnlinePlayer player, out LoreCard card)
    {
        card = LoreCard.Blank;
        if (player.Character is not { } character)
            return LoreCardState.NoCharacter;

        var state = _cards.Find(character.Id, out card);
        if (state == LoreCardState.Ready)
            card = _cards.FindOrStart(character.Id);

        return state;
    }

    // Why a card cannot change now, null when it can.
    public static Phrase? FindRefusal(LoreCardState state) => state switch
    {
        LoreCardState.Ready => null,
        LoreCardState.Loading => LoreText.Loading,
        LoreCardState.Unreadable => LoreText.Unreadable,
        LoreCardState.NoCharacter => ChatText.NeedCharacter,
    };

    // Private until the card has loaded.
    public bool IsNamePublic(OnlinePlayer player) =>
        player.Character is { } character && _cards.Find(character.Id, out var card) == LoreCardState.Ready && card.NamePublic;

    // Only an edit that went through: its card takes the old one's place and its fields are written.
    public void Save(OnlinePlayer player, LoreEdit edit)
    {
        if (!edit.Ok || player.Character is not { } character)
            throw new InvalidOperationException("Only an accepted edit of a character's card is saved.");

        _cards.Put(character.Id, edit.Card);
        var now = clock.UtcNow;
        var rows = edit.Changed.Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(id => ToRow(character.Id, LoreFields.Get(id), edit.Card, now))
            .ToList();
        var characterId = character.Id;
        fire.Run(SaveJob, () => persistence.Writer.EnqueueAsync(SaveJob, (db, ct) => LoreBook.SaveFieldsAsync(db, characterId, rows, ct)));
    }

    // Only for a card that is ready.
    public void SetNamePublic(OnlinePlayer player, bool namePublic)
    {
        if (Find(player, out var card) != LoreCardState.Ready || player.Character is not { } character)
            throw new InvalidOperationException("Only a character whose card is ready shows or hides its name.");

        _cards.Put(character.Id, card with { NamePublic = namePublic });
        var characterId = character.Id;
        fire.Run(NameJob, () => persistence.Writer.EnqueueAsync(NameJob, (db, ct) => LoreBook.SetNamePublicAsync(db, characterId, namePublic, ct)));
        callbacks.Raise("lore.name_shown", NamePublicChanged, player);
    }

    public static LoreCard ToCard(LoreCardRecord record)
    {
        var values = ImmutableDictionary.CreateBuilder<string, LoreValue>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in record.Fields)
            values[row.FieldId] = new LoreValue(row.CatalogId, row.Text, row.Number, row.Fixed, LoreRollFormat.Read(row.Rolls), row.Visibility);

        return new LoreCard(record.NamePublic, values.ToImmutable());
    }

    public static LoreField ToRow(long characterId, LoreFieldDefinition field, LoreCard card, DateTimeOffset now)
    {
        var value = card.Get(field) ?? LoreValue.Empty(field.DefaultVisibility);
        return new LoreField
        {
            CharacterId = characterId,
            FieldId = field.Id,
            CatalogId = value.CatalogId,
            Text = value.Text,
            Number = value.Number,
            Fixed = value.Fixed,
            Rolls = LoreRollFormat.Store(value.Rolls),
            Visibility = value.Visibility,
            UpdatedAt = now,
        };
    }

    private void Start()
    {
        online.Loaded += OnLoaded;
        online.Left += OnLeft;
    }

    private void OnLoaded(OnlinePlayer player)
    {
        if (player.Character is not { } character)
            return;

        var characterId = character.Id;
        _cards.BeginLoading(characterId);
        fire.Run(LoadJob, async () =>
        {
            LoreCard card;
            try
            {
                card = ToCard(await persistence.Reader.RunAsync((db, ct) => LoreBook.LoadCardAsync(db, characterId, ct)));
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "[CyrodiilRP] The lore card of character {Character} could not be read.", characterId);
                if (player.IsLive)
                    _cards.MarkUnreadable(characterId);

                return;
            }

            if (player.IsLive && _cards.IsLoading(characterId))
            {
                _cards.Put(characterId, card);
                if (card.NamePublic)
                    callbacks.Raise("lore.name_shown", NamePublicChanged, player);
            }
        });
    }

    private void OnLeft(OnlinePlayer player)
    {
        if (player.Character is { } character)
            _cards.Remove(character.Id);
    }
}
