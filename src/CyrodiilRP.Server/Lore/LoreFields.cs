using CyrodiilRP.Data;
using CyrodiilRP.Server.Localization;

namespace CyrodiilRP.Server.Lore;

// MESOL's card without the first and last name, which are the character's own. MESOL's Known, Friends and Private
// defaults are Private here, as the mode knows only introductions, and the notes stay the owner's alone.
internal static class LoreFields
{
    public const string Title = "title";
    public const string SeenAs = "seenAs";
    public const string Race = "race";
    public const string Subtype = "subtype";
    public const string Homeland = "homeland";
    public const string HomeRegion = "homeRegion";
    public const string Culture = "culture";
    public const string Upbringing = "upbringing";
    public const string Affiliation = "affiliation";
    public const string Rank = "rank";
    public const string Status = "icOoc";

    // What 5d20 can give.
    public const int TraitMin = 5;
    public const int TraitMax = 100;
    public const int TraitDice = 5;

    // All the text of one card.
    public const int MaxCardCharacters = 16_000;

    // Under the column's length, so a field id never needs cutting.
    public const int MaxIdLength = 32;

    public static IReadOnlyList<LoreFieldDefinition> All { get; } =
    [
        new("title", LorePart.Identity, LoreText.FieldTitle, LoreText.HintTitle, LoreFieldType.Text, 30, LoreVisibility.Private),
        new("fullTitle", LorePart.Identity, LoreText.FieldFullTitle, LoreText.HintFullTitle, LoreFieldType.Text, 80, LoreVisibility.Private),
        new("seenAs", LorePart.Identity, LoreText.FieldSeenAs, LoreText.HintSeenAs, LoreFieldType.Text, 80, LoreVisibility.Public) { Locked = true },
        new("race", LorePart.Heritage, LoreText.FieldRace, LoreText.HintRace, LoreFieldType.Catalog, 30, LoreVisibility.Public) { Kinds = [LoreKind.Race] },
        new("subtype", LorePart.Heritage, LoreText.FieldSubtype, LoreText.HintSubtype, LoreFieldType.Catalog, 60, LoreVisibility.Public) { Kinds = [LoreKind.Subtype, LoreKind.Heritage], RaceScoped = true, AllowHistorical = true, Needs = Race },
        new("homeland", LorePart.Heritage, LoreText.FieldHomeland, LoreText.HintHomeland, LoreFieldType.Catalog, 60, LoreVisibility.Private) { Kinds = [LoreKind.Province] },
        new("homeRegion", LorePart.Heritage, LoreText.FieldHomeRegion, LoreText.HintHomeRegion, LoreFieldType.Catalog, 60, LoreVisibility.Private) { Kinds = [LoreKind.Region, LoreKind.Settlement], Needs = Homeland },
        new("culture", LorePart.Heritage, LoreText.FieldCulture, LoreText.HintCulture, LoreFieldType.Catalog, 60, LoreVisibility.Private) { Kinds = [LoreKind.Culture, LoreKind.House, LoreKind.Tribe], RaceScoped = true, AllowHistorical = true, Needs = Race },
        new("upbringing", LorePart.Heritage, LoreText.FieldUpbringing, LoreText.HintUpbringing, LoreFieldType.Catalog, 60, LoreVisibility.Private) { Kinds = [LoreKind.Upbringing], RaceScoped = true },
        new("standing", LorePart.Heritage, LoreText.FieldStanding, LoreText.HintStanding, LoreFieldType.Catalog, 40, LoreVisibility.Private) { Kinds = [LoreKind.Standing] },
        new("occupation", LorePart.Heritage, LoreText.FieldOccupation, LoreText.HintOccupation, LoreFieldType.Catalog, 40, LoreVisibility.Private) { Kinds = [LoreKind.Occupation] },
        new("family", LorePart.Heritage, LoreText.FieldFamily, LoreText.HintFamily, LoreFieldType.Text, 200, LoreVisibility.Private),
        new("ancestry", LorePart.Heritage, LoreText.FieldAncestry, LoreText.HintAncestry, LoreFieldType.Text, 100, LoreVisibility.Private),
        new("birthsign", LorePart.Life, LoreText.FieldBirthsign, LoreText.HintBirthsign, LoreFieldType.Catalog, 30, LoreVisibility.Private) { Kinds = [LoreKind.Birthsign] },
        new("birthday", LorePart.Life, LoreText.FieldBirthday, LoreText.HintBirthday, LoreFieldType.Text, 30, LoreVisibility.Private),
        new("age", LorePart.Life, LoreText.FieldAge, LoreText.HintAge, LoreFieldType.Text, 30, LoreVisibility.Private),
        new("birthplace", LorePart.Life, LoreText.FieldBirthplace, LoreText.HintBirthplace, LoreFieldType.Catalog, 60, LoreVisibility.Private) { Kinds = [LoreKind.Settlement, LoreKind.Region, LoreKind.Province, LoreKind.Homeland] },
        new("class", LorePart.Life, LoreText.FieldClass, LoreText.HintClass, LoreFieldType.Catalog, 30, LoreVisibility.Private) { Kinds = [LoreKind.Class] },
        new("faith", LorePart.Life, LoreText.FieldFaith, LoreText.HintFaith, LoreFieldType.Catalog, 60, LoreVisibility.Private) { Kinds = [LoreKind.Faith, LoreKind.Deity] },
        new("affiliation", LorePart.Life, LoreText.FieldAffiliation, LoreText.HintAffiliation, LoreFieldType.Catalog, 60, LoreVisibility.Private) { Kinds = [LoreKind.Faction] },
        new("rank", LorePart.Life, LoreText.FieldRank, LoreText.HintRank, LoreFieldType.Catalog, 40, LoreVisibility.Private) { Kinds = [LoreKind.Rank, LoreKind.Title], Needs = Affiliation },
        new("relationship", LorePart.Life, LoreText.FieldRelationship, LoreText.HintRelationship, LoreFieldType.Choice, 20, LoreVisibility.Private) { Choices = [new("unwed", LoreText.ChoiceUnwed), new("courting", LoreText.ChoiceCourting), new("betrothed", LoreText.ChoiceBetrothed), new("wed", LoreText.ChoiceWed), new("widowed", LoreText.ChoiceWidowed), new("estranged", LoreText.ChoiceEstranged), new("complicated", LoreText.ChoiceComplicated)] },
        new("height", LorePart.Look, LoreText.FieldHeight, LoreText.HintHeight, LoreFieldType.Text, 40, LoreVisibility.Public),
        new("build", LorePart.Look, LoreText.FieldBuild, LoreText.HintBuild, LoreFieldType.Text, 40, LoreVisibility.Public),
        new("eyes", LorePart.Look, LoreText.FieldEyes, LoreText.HintEyes, LoreFieldType.Text, 40, LoreVisibility.Public),
        new("hair", LorePart.Look, LoreText.FieldHair, LoreText.HintHair, LoreFieldType.Text, 40, LoreVisibility.Public),
        new("marks", LorePart.Look, LoreText.FieldMarks, LoreText.HintMarks, LoreFieldType.Text, 80, LoreVisibility.Public),
        new("glance", LorePart.Look, LoreText.FieldGlance, LoreText.HintGlance, LoreFieldType.Text, 300, LoreVisibility.Public),
        new("physical", LorePart.Story, LoreText.FieldPhysical, LoreText.HintPhysical, LoreFieldType.LongText, 2000, LoreVisibility.Private),
        new("personality", LorePart.Story, LoreText.FieldPersonality, LoreText.HintPersonality, LoreFieldType.LongText, 2000, LoreVisibility.Private),
        new("history", LorePart.Story, LoreText.FieldHistory, LoreText.HintHistory, LoreFieldType.LongText, 2000, LoreVisibility.Private),
        new("notes", LorePart.Story, LoreText.FieldNotes, LoreText.HintNotes, LoreFieldType.LongText, 2000, LoreVisibility.Private) { Locked = true, OwnerOnly = true },
        new("icOoc", LorePart.Status, LoreText.FieldIcOoc, LoreText.HintIcOoc, LoreFieldType.Choice, 10, LoreVisibility.Public) { Locked = true, Choices = [new("ic", LoreText.ChoiceIc), new("ooc", LoreText.ChoiceOoc), new("lfrp", LoreText.ChoiceLfrp)] },
        new("currently", LorePart.Status, LoreText.FieldCurrently, LoreText.HintCurrently, LoreFieldType.Text, 200, LoreVisibility.Public),
        new("oocInfo", LorePart.Status, LoreText.FieldOocInfo, LoreText.HintOocInfo, LoreFieldType.Text, 300, LoreVisibility.Public),
        new("consent.injury", LorePart.Status, LoreText.FieldConsentInjury, LoreText.HintConsentInjury, LoreFieldType.Choice, 10, LoreVisibility.Public) { Locked = true, Choices = [new("no", LoreText.ChoiceNo), new("ask", LoreText.ChoiceAsk), new("yes", LoreText.ChoiceYes)] },
        new("consent.death", LorePart.Status, LoreText.FieldConsentDeath, LoreText.HintConsentDeath, LoreFieldType.Choice, 10, LoreVisibility.Public) { Locked = true, Choices = [new("no", LoreText.ChoiceNo), new("ask", LoreText.ChoiceAsk), new("yes", LoreText.ChoiceYes)] },
        new("consent.romance", LorePart.Status, LoreText.FieldConsentRomance, LoreText.HintConsentRomance, LoreFieldType.Choice, 10, LoreVisibility.Public) { Locked = true, Choices = [new("no", LoreText.ChoiceNo), new("ask", LoreText.ChoiceAsk), new("yes", LoreText.ChoiceYes)] },
        new("consent.crime", LorePart.Status, LoreText.FieldConsentCrime, LoreText.HintConsentCrime, LoreFieldType.Choice, 10, LoreVisibility.Public) { Locked = true, Choices = [new("no", LoreText.ChoiceNo), new("ask", LoreText.ChoiceAsk), new("yes", LoreText.ChoiceYes)] },
        new("consent.control", LorePart.Status, LoreText.FieldConsentControl, LoreText.HintConsentControl, LoreFieldType.Choice, 10, LoreVisibility.Public) { Locked = true, Choices = [new("no", LoreText.ChoiceNo), new("ask", LoreText.ChoiceAsk), new("yes", LoreText.ChoiceYes)] },
        new("experience", LorePart.Status, LoreText.FieldExperience, LoreText.HintExperience, LoreFieldType.Choice, 10, LoreVisibility.Public) { Choices = [new("newcomer", LoreText.ChoiceNewcomer), new("casual", LoreText.ChoiceCasual), new("veteran", LoreText.ChoiceVeteran), new("guide", LoreText.ChoiceGuide)] },
        new("trait.fearful", LorePart.Traits, LoreText.FieldTraitFearful, LoreText.HintTraitFearful, LoreFieldType.Trait, 3, LoreVisibility.Private) { Group = LoreText.GroupTemperament },
        new("trait.hotTempered", LorePart.Traits, LoreText.FieldTraitHotTempered, LoreText.HintTraitHotTempered, LoreFieldType.Trait, 3, LoreVisibility.Private) { Group = LoreText.GroupTemperament },
        new("trait.melancholic", LorePart.Traits, LoreText.FieldTraitMelancholic, LoreText.HintTraitMelancholic, LoreFieldType.Trait, 3, LoreVisibility.Private) { Group = LoreText.GroupTemperament },
        new("trait.reckless", LorePart.Traits, LoreText.FieldTraitReckless, LoreText.HintTraitReckless, LoreFieldType.Trait, 3, LoreVisibility.Private) { Group = LoreText.GroupTemperament },
        new("trait.stubborn", LorePart.Traits, LoreText.FieldTraitStubborn, LoreText.HintTraitStubborn, LoreFieldType.Trait, 3, LoreVisibility.Private) { Group = LoreText.GroupTemperament },
        new("trait.patient", LorePart.Traits, LoreText.FieldTraitPatient, LoreText.HintTraitPatient, LoreFieldType.Trait, 3, LoreVisibility.Private) { Group = LoreText.GroupTemperament },
        new("trait.romantic", LorePart.Traits, LoreText.FieldTraitRomantic, LoreText.HintTraitRomantic, LoreFieldType.Trait, 3, LoreVisibility.Private) { Group = LoreText.GroupHeart },
        new("trait.jealous", LorePart.Traits, LoreText.FieldTraitJealous, LoreText.HintTraitJealous, LoreFieldType.Trait, 3, LoreVisibility.Private) { Group = LoreText.GroupHeart },
        new("trait.loyal", LorePart.Traits, LoreText.FieldTraitLoyal, LoreText.HintTraitLoyal, LoreFieldType.Trait, 3, LoreVisibility.Private) { Group = LoreText.GroupHeart },
        new("trait.merciful", LorePart.Traits, LoreText.FieldTraitMerciful, LoreText.HintTraitMerciful, LoreFieldType.Trait, 3, LoreVisibility.Private) { Group = LoreText.GroupHeart },
        new("trait.vengeful", LorePart.Traits, LoreText.FieldTraitVengeful, LoreText.HintTraitVengeful, LoreFieldType.Trait, 3, LoreVisibility.Private) { Group = LoreText.GroupHeart },
        new("trait.lustful", LorePart.Traits, LoreText.FieldTraitLustful, LoreText.HintTraitLustful, LoreFieldType.Trait, 3, LoreVisibility.Private) { Group = LoreText.GroupHeart },
        new("trait.lazy", LorePart.Traits, LoreText.FieldTraitLazy, LoreText.HintTraitLazy, LoreFieldType.Trait, 3, LoreVisibility.Private) { Group = LoreText.GroupVices },
        new("trait.slovenly", LorePart.Traits, LoreText.FieldTraitSlovenly, LoreText.HintTraitSlovenly, LoreFieldType.Trait, 3, LoreVisibility.Private) { Group = LoreText.GroupVices },
        new("trait.gluttonous", LorePart.Traits, LoreText.FieldTraitGluttonous, LoreText.HintTraitGluttonous, LoreFieldType.Trait, 3, LoreVisibility.Private) { Group = LoreText.GroupVices },
        new("trait.greedy", LorePart.Traits, LoreText.FieldTraitGreedy, LoreText.HintTraitGreedy, LoreFieldType.Trait, 3, LoreVisibility.Private) { Group = LoreText.GroupVices },
        new("trait.fondOfDrink", LorePart.Traits, LoreText.FieldTraitFondOfDrink, LoreText.HintTraitFondOfDrink, LoreFieldType.Trait, 3, LoreVisibility.Private) { Group = LoreText.GroupVices },
        new("trait.superstitious", LorePart.Traits, LoreText.FieldTraitSuperstitious, LoreText.HintTraitSuperstitious, LoreFieldType.Trait, 3, LoreVisibility.Private) { Group = LoreText.GroupVices },
        new("trait.proud", LorePart.Traits, LoreText.FieldTraitProud, LoreText.HintTraitProud, LoreFieldType.Trait, 3, LoreVisibility.Private) { Group = LoreText.GroupMind },
        new("trait.deceitful", LorePart.Traits, LoreText.FieldTraitDeceitful, LoreText.HintTraitDeceitful, LoreFieldType.Trait, 3, LoreVisibility.Private) { Group = LoreText.GroupMind },
        new("trait.suspicious", LorePart.Traits, LoreText.FieldTraitSuspicious, LoreText.HintTraitSuspicious, LoreFieldType.Trait, 3, LoreVisibility.Private) { Group = LoreText.GroupMind },
        new("trait.curious", LorePart.Traits, LoreText.FieldTraitCurious, LoreText.HintTraitCurious, LoreFieldType.Trait, 3, LoreVisibility.Private) { Group = LoreText.GroupMind },
        new("trait.pious", LorePart.Traits, LoreText.FieldTraitPious, LoreText.HintTraitPious, LoreFieldType.Trait, 3, LoreVisibility.Private) { Group = LoreText.GroupMind },
        new("trait.honourable", LorePart.Traits, LoreText.FieldTraitHonourable, LoreText.HintTraitHonourable, LoreFieldType.Trait, 3, LoreVisibility.Private) { Group = LoreText.GroupMind },
    ];

    // A new race or homeland empties the picks that went with the old one.
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Dependents { get; } = new Dictionary<string, IReadOnlyList<string>>
    {
        [Race] = [Subtype, Culture, Upbringing],
        [Homeland] = [HomeRegion],
    };

    private static readonly Dictionary<string, LoreFieldDefinition> ById = All.ToDictionary(field => field.Id, StringComparer.OrdinalIgnoreCase);

    public static bool TryGet(string? id, out LoreFieldDefinition field)
    {
        if (id is not null && ById.TryGetValue(id.Trim(), out var found))
        {
            field = found;
            return true;
        }

        field = null!;
        return false;
    }

    public static LoreFieldDefinition Get(string id) =>
        TryGet(id, out var field) ? field : throw new ArgumentOutOfRangeException(nameof(id), id, "No such lore field.");

    // How strongly a trait drives the character.
    public static Phrase TraitWord(int value) => value switch
    {
        <= 20 => LoreText.WordFaintly,
        <= 40 => LoreText.WordSomewhat,
        <= 60 => LoreText.WordNotably,
        <= 80 => LoreText.WordStrongly,
        _ => LoreText.WordOverwhelmingly,
    };
}
