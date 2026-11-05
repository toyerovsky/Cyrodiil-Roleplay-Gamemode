using System.Text;
using CyrodiilRP.Data;
using CyrodiilRP.Server.Localization;

namespace CyrodiilRP.Server.Lore;

// Every rule for changing a card: lengths, choices, the catalog mode, the era rule (3E 433), what fits the game's one body
// per race, and traits typed once or rolled with one reroll. It changes nothing itself: an edit gives a new card.
internal sealed class LoreEditor(LoreCatalog catalog, LoreCatalogMode mode, bool allowImplausible)
{
    // The most entries one picker gets: the longest list, the places, is under 250.
    public const int MaxOptions = 400;

    private const string ListSeparator = ", ";
    // A settlement in a region in a province, and room to spare.
    private const int MaxPlaceDepth = 6;

    private readonly LoreCardText _text = new(catalog, markCustom: mode == LoreCatalogMode.CanonPlus);

    public LoreCatalogMode Mode => mode;

    // On a canon server the fields of the lore list hold picks only: words typed there before go, the visibility stays.
    // Null when there were none.
    public LoreEdit? DropCustom(LoreCard card)
    {
        if (mode != LoreCatalogMode.Canon)
            return null;

        List<string> changed = [];
        foreach (var field in LoreFields.All.Where(f => f.Type == LoreFieldType.Catalog))
        {
            if (card.Get(field) is { Text.Length: > 0 } value)
            {
                card = card.With(field, value with { Text = null });
                changed.Add(field.Id);
            }
        }

        return changed.Count == 0 ? null : new LoreEdit(true, LoreText.CustomDropped, card, changed);
    }

    public LoreEdit Set(LoreCard card, LoreFieldDefinition field, string text)
    {
        var clean = Clean(text, field.Type == LoreFieldType.LongText);
        if (clean.Length == 0)
            return LoreEdit.Refused(card, LoreText.NothingToSet(field.Label));

        var old = card.Get(field);
        var visibility = old?.Visibility ?? field.DefaultVisibility;
        switch (field.Type)
        {
            case LoreFieldType.Trait:
                return SetTrait(card, field, old, clean);

            case LoreFieldType.Choice:
                if (LoreCardText.FindChoice(field, clean) is not { } choice)
                    return LoreEdit.Refused(card, LoreText.ChoiceOneOf(field.Label, string.Join(ListSeparator, field.Choices.Select(c => c.Id))));

                return Store(card, field, old, LoreValue.Empty(visibility) with { Text = choice.Id });

            case LoreFieldType.Catalog:
                if (mode == LoreCatalogMode.Canon)
                    return LoreEdit.Refused(card, LoreText.PickedOnly(field.Label));

                // Beside a pick the words add to it ("Anequina" and "Rimmen outskirts"), alone they stand in for one.
                return Store(card, field, old, LoreValue.Empty(visibility) with { CatalogId = old?.CatalogId, Text = clean });

            case LoreFieldType.Text:
            case LoreFieldType.LongText:
                return Store(card, field, old, LoreValue.Empty(visibility) with { Text = clean });

            default:
                throw new ArgumentOutOfRangeException(nameof(field), field.Type, "No such field type.");
        }
    }

    public LoreEdit Clear(LoreCard card, LoreFieldDefinition field)
    {
        var old = card.Get(field);
        if (old is null || old.IsEmpty)
            return LoreEdit.Refused(card, LoreText.AlreadyEmpty(field.Label));

        if (field.Type == LoreFieldType.Trait)
            return LoreEdit.Refused(card, LoreText.TraitStaffOnly(field.Label));

        return Cleared(card, field, old);
    }

    // The staff take out what should not be on a card, a trait too.
    public LoreEdit ClearForStaff(LoreCard card, LoreFieldDefinition field)
    {
        var old = card.Get(field);
        return old is null || old.IsEmpty ? LoreEdit.Refused(card, LoreText.AlreadyEmpty(field.Label)) : Cleared(card, field, old);
    }

    public LoreEdit SetVisibility(LoreCard card, LoreFieldDefinition field, LoreVisibility visibility)
    {
        if (field.OwnerOnly)
            return LoreEdit.Refused(card, LoreText.LockedOwner(field.Label));

        if (field.Locked)
            return LoreEdit.Refused(card, LoreText.LockedAlways(field.Label, Word(field.DefaultVisibility)));

        var value = card.Get(field) is { } old ? old with { Visibility = visibility } : LoreValue.Empty(visibility);
        return new LoreEdit(true, LoreText.VisibilitySet(field.Label, Word(visibility)), card.With(field, value), [field.Id]);
    }

    // Null when the trait may be rolled now: never set (the first roll), or rolled once and not kept (the reroll).
    public Phrase? CheckRoll(LoreCard card, LoreFieldDefinition field, out LoreRollKind kind)
    {
        kind = LoreRollKind.First;
        if (field.Type != LoreFieldType.Trait)
            return LoreText.NotTrait(field.Label);

        var old = card.Get(field);
        if (old?.Number is null)
            return null;

        if (!old.Fixed && old.Rolls.Count == 1)
        {
            kind = LoreRollKind.Reroll;
            return null;
        }

        return old.Rolls.Count > 0 ? LoreText.NoMoreRerolls(field.Label) : LoreText.TypedForGood(field.Label);
    }

    // A roll the server made. CheckRoll comes first, so no roll is thrown away.
    public LoreEdit ApplyRoll(LoreCard card, LoreFieldDefinition field, IReadOnlyList<int> dice)
    {
        if (CheckRoll(card, field, out var kind) is { } refused)
            return LoreEdit.Refused(card, refused);

        var total = dice.Sum();
        if (dice.Count != LoreFields.TraitDice || total is < LoreFields.TraitMin or > LoreFields.TraitMax)
            throw new ArgumentException($"A trait takes {LoreFields.TraitDice}d20, not {string.Join(ListSeparator, dice)}.", nameof(dice));

        var old = card.Get(field);
        var reroll = kind == LoreRollKind.Reroll;
        var value = new LoreValue(null, null, total, reroll, [.. old?.Rolls ?? [], new LoreTraitRoll(dice.ToList(), total)],
            old?.Visibility ?? field.DefaultVisibility);
        var word = LoreFields.TraitWord(total);
        var message = reroll ? LoreText.Rerolled(field.Label, total, word) : LoreText.Rolled(field.Label, total, word);
        return new LoreEdit(true, message, card.With(field, value), [field.Id]);
    }

    // The first roll kept for good, the reroll given up.
    public LoreEdit Keep(LoreCard card, LoreFieldDefinition field)
    {
        var old = card.Get(field);
        if (field.Type != LoreFieldType.Trait || old?.Number is not { } number)
            return LoreEdit.Refused(card, LoreText.NothingToKeep(field.Label));

        if (old.Fixed)
            return LoreEdit.Refused(card, LoreText.AlreadyKept(field.Label));

        return new LoreEdit(true, LoreText.Kept(field.Label, number), card.With(field, old with { Fixed = true }), [field.Id]);
    }

    // The entries this card may pick for the field. Problem: why there are none, null when there are some.
    public List<LoreEntry> Candidates(LoreCard card, LoreFieldDefinition field, string query, int max, out Phrase? problem)
    {
        problem = null;
        if (field.Type != LoreFieldType.Catalog)
        {
            problem = LoreText.NotPicked(field.Label);
            return [];
        }

        var race = FindRace(card);
        if (field.Needs is { } needed && card.Get(LoreFields.Get(needed)) is not { CatalogId: not null })
        {
            problem = LoreText.PickFirst(LoreFields.Get(needed).Label);
            return [];
        }

        var found = catalog.Search(field.Kinds, query, entry => IsAllowed(card, field, entry, race), max);
        if (found.Count == 0)
            problem = LoreText.NothingFits(field.Label);

        return found;
    }

    public LoreEdit Pick(LoreCard card, LoreFieldDefinition field, LoreEntry entry)
    {
        if (field.Type != LoreFieldType.Catalog || !field.Kinds.Contains(entry.Kind) || !IsAllowed(card, field, entry, FindRace(card)))
            return LoreEdit.Refused(card, LoreText.DoesNotFit(entry.Name, field.Label));

        var old = card.Get(field);
        var stored = Store(card, field, old, LoreValue.Empty(old?.Visibility ?? field.DefaultVisibility) with { CatalogId = entry.Id });
        if (!stored.Ok || old?.CatalogId is null || old.CatalogId == entry.Id || !LoreFields.Dependents.TryGetValue(field.Id, out var dependents))
            return stored;

        var next = stored.Card;
        List<string> changed = [.. stored.Changed];
        List<string> cleared = [];
        foreach (var bound in dependents.Select(LoreFields.Get))
        {
            if (next.Get(bound) is { IsEmpty: false } value)
            {
                next = next.With(bound, LoreValue.Empty(value.Visibility));
                changed.Add(bound.Id);
                cleared.Add(bound.Id);
            }
        }

        if (cleared.Count == 0)
            return stored;

        var labels = cleared.Select(id => LoreFields.Get(id).Label).ToList();
        var list = labels.Count switch
        {
            1 => labels[0],
            2 => LoreText.Two(labels[0], labels[1]),
            3 => LoreText.Three(labels[0], labels[1], labels[2]),
            _ => throw new InvalidOperationException($"{field.Id} has more dependent fields than a sentence lists."),
        };
        return new LoreEdit(true, LoreText.PickedAndCleared(stored.Message, list), next, changed);
    }

    // The race's lore name the lore list uses ("Khajiit"), from the card's pick.
    public string? FindRace(LoreCard card) =>
        card.Get(LoreFields.Get(LoreFields.Race))?.CatalogId is { } id && catalog.TryGet(id, out var entry) ? entry.Race ?? entry.Name : null;

    // The province a race calls home, for its own lands first in the homeland picker. A race of no province has none.
    public static string? FindNativeProvince(string? race) => race?.ToLowerInvariant() switch
    {
        "altmer" => "province.summerset-isles",
        "argonian" => "province.black-marsh",
        "bosmer" => "province.valenwood",
        "breton" => "province.high-rock",
        "dunmer" => "province.morrowind",
        "imperial" => "province.cyrodiil",
        "khajiit" => "province.elsweyr",
        "nord" => "province.skyrim",
        "orsimer" => "province.orsinium",
        "redguard" => "province.hammerfell",
        _ => null,
    };

    // The place itself, or a place under it.
    public bool IsInside(LoreEntry entry, string placeId)
    {
        LoreEntry? at = entry;
        for (var depth = 0; at is not null && depth < MaxPlaceDepth; depth++)
        {
            if (string.Equals(at.Id, placeId, StringComparison.OrdinalIgnoreCase))
                return true;

            at = at.Parent is { } parent && catalog.TryGet(parent, out var up) ? up : null;
        }

        return false;
    }

    // Without control characters, a line field without line breaks too, and no runs of spaces.
    public static string Clean(string text, bool multiline)
    {
        var builder = new StringBuilder(text.Length);
        var space = false;
        foreach (var c in text)
        {
            var isBreak = c is '\r' or '\n';
            if (char.IsControl(c) && !(multiline && isBreak))
            {
                if ((isBreak || c == '\t') && !space)
                {
                    builder.Append(' ');
                    space = true;
                }

                continue;
            }

            if (c == ' ')
            {
                if (space)
                    continue;

                space = true;
            }
            else
            {
                space = false;
            }

            builder.Append(c);
        }

        return builder.ToString().Trim();
    }

    public static Phrase Word(LoreVisibility visibility) => visibility switch
    {
        LoreVisibility.Public => LoreText.VisPublic,
        LoreVisibility.Private => LoreText.VisPrivate,
    };

    private LoreEdit SetTrait(LoreCard card, LoreFieldDefinition field, LoreValue? old, string text)
    {
        if (old?.Number is not null)
            return LoreEdit.Refused(card, old.Fixed ? LoreText.AlreadyKept(field.Label) : LoreText.RolledKeepOrReroll(field.Label));

        if (!int.TryParse(text, out var number) || number is < LoreFields.TraitMin or > LoreFields.TraitMax)
            return LoreEdit.Refused(card, LoreText.TraitRange(field.Label, LoreFields.TraitMin, LoreFields.TraitMax));

        var value = new LoreValue(null, null, number, true, [], old?.Visibility ?? field.DefaultVisibility);
        return new LoreEdit(true, LoreText.TraitTyped(field.Label, number, LoreFields.TraitWord(number)), card.With(field, value), [field.Id]);
    }

    private LoreEdit Cleared(LoreCard card, LoreFieldDefinition field, LoreValue old) =>
        new(true, LoreText.Cleared(field.Label), card.With(field, LoreValue.Empty(old.Visibility)), [field.Id]);

    // The era rule, the one body per race, the race's own entries, a home region inside the homeland and a rank of the
    // affiliation.
    private bool IsAllowed(LoreCard card, LoreFieldDefinition field, LoreEntry entry, string? race)
    {
        if (entry.Era == LoreEra.Future || (entry.Era == LoreEra.Historical && !field.AllowHistorical))
            return false;

        if (entry.ModelFit == LoreModelFit.Implausible && !allowImplausible)
            return false;

        if (string.Equals(entry.Race, LoreEntry.MixedRace, StringComparison.OrdinalIgnoreCase))
            return false;

        if (field.RaceScoped && entry.Race is not null && !string.Equals(entry.Race, race, StringComparison.OrdinalIgnoreCase))
            return false;

        if (field.Id == LoreFields.HomeRegion)
            return card.Get(LoreFields.Get(LoreFields.Homeland))?.CatalogId is { } province && IsInside(entry, province);

        if (field.Id == LoreFields.Rank)
        {
            var faction = card.Get(LoreFields.Get(LoreFields.Affiliation))?.CatalogId;
            return (faction is not null && entry.Parent == faction) || (entry.Kind == LoreKind.Title && entry.Parent is null);
        }

        return true;
    }

    private LoreEdit Store(LoreCard card, LoreFieldDefinition field, LoreValue? old, LoreValue value)
    {
        var length = value.Text?.Length ?? 0;
        if (length > field.MaxLength)
            return LoreEdit.Refused(card, LoreText.TooLong(field.Label, field.MaxLength, length));

        var total = card.TextLength - (old?.Text?.Length ?? 0) + length;
        if (total > LoreFields.MaxCardCharacters)
            return LoreEdit.Refused(card, LoreText.CardFull(total, LoreFields.MaxCardCharacters));

        var message = field.Type == LoreFieldType.LongText
            ? LoreText.Saved(field.Label, Word(value.Visibility))
            : LoreText.Stored(field.Label, _text.Display(field, value), Word(value.Visibility));
        return new LoreEdit(true, message, card.With(field, value), [field.Id]);
    }
}
