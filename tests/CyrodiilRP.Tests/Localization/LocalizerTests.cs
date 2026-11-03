using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Tests.Fakes;

namespace CyrodiilRP.Tests.Localization;

public sealed partial class LocalizerTests
{
    // The server's locales, copied beside the server half and so beside the tests.
    private static readonly string Shipped = Path.Combine(AppContext.BaseDirectory, "locales");

    private static readonly Localizer Texts = Localizer.LoadFolder(Shipped, new ListLogger());

    // Counts enough to meet every plural category a language has for whole numbers.
    private const int PluralSample = 200;

    [Fact]
    public void A_phrase_is_filled_in_the_readers_language()
    {
        Assert.Equal("Marius Varro says: Good morning.", Texts.Get("en", ChatText.Say("Marius Varro", "Good morning.")));
    }

    [Fact]
    public void An_account_locale_the_server_lacks_is_read_as_English()
    {
        Assert.Equal(Localizer.BaseLocale, Texts.PickSupported("xx"));
        Assert.Equal("pl", Texts.PickSupported("pl"));
    }

    [Theory]
    [InlineData(1, "You carry 1 gold coin.")]
    [InlineData(5, "You carry 5 gold coins.")]
    [InlineData(0, "You carry 0 gold coins.")]
    public void A_count_picks_the_plural_form(int count, string expected) =>
        Assert.Equal(expected, Texts.Get("en", EconomyText.Carrying(count)));

    [Theory]
    [InlineData(1, "Masz przy sobie 1 sztukę złota.")]
    [InlineData(3, "Masz przy sobie 3 sztuki złota.")]
    [InlineData(5, "Masz przy sobie 5 sztuk złota.")]
    [InlineData(12, "Masz przy sobie 12 sztuk złota.")]
    [InlineData(22, "Masz przy sobie 22 sztuki złota.")]
    public void A_Polish_count_picks_the_Polish_form(int count, string expected) =>
        Assert.Equal(expected, Texts.Get("pl", EconomyText.Carrying(count)));

    [Fact]
    public void A_phrase_inside_a_phrase_is_written_in_the_same_language()
    {
        Assert.Equal("Last name must be 2 to 20 characters long.",
            Texts.Get("en", CharactersText.NameLength(CharactersText.LastName, 2, 20)));
    }

    [Fact]
    public void A_players_own_words_are_never_read_as_a_placeholder()
    {
        Assert.Equal("* {name} is on the sign. (( Marius Varro ))",
            Texts.Get("en", ChatText.Do("{name} is on the sign.", "Marius Varro")));
    }

    [Fact]
    public void A_missing_text_is_logged_once_for_each_locale_and_reads_as_its_key()
    {
        var logger = new ListLogger();
        var texts = Localizer.LoadFolder(Shipped, logger);
        var missing = new Phrase("chat.no_such_text");

        Assert.Equal(missing.Key, texts.Get("en", missing));
        Assert.Equal(missing.Key, texts.Get("en", missing));
        Assert.Equal(missing.Key, texts.Get("pl", missing));
        Assert.Equal(ChatText.Loading.Key, texts.Get("xx", ChatText.Loading));

        Assert.Equal(3, logger.Errors.Count());
    }

    [Fact]
    public void A_plural_without_the_form_its_count_picks_is_logged_and_not_read_in_another_form()
    {
        var root = Directory.CreateTempSubdirectory("locales").FullName;
        try
        {
            foreach (var locale in new[] { "en", "pl" })
            {
                Directory.CreateDirectory(Path.Combine(root, locale));
                File.WriteAllText(Path.Combine(root, locale, "chat.json"), """{ "chat.coins": { "one": "{count} coin", "other": "{count} coins" } }""");
            }

            var logger = new ListLogger();
            var texts = Localizer.LoadFolder(root, logger);
            var phrase = new Phrase("chat.coins", new TextArg("count", 3));

            Assert.Equal("3 coins", texts.Get("en", phrase));
            Assert.Equal(phrase.Key, texts.Get("pl", phrase));
            Assert.Contains("few", Assert.Single(logger.Errors).Text, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Every_generated_text_is_in_every_language()
    {
        var logger = new ListLogger();
        var texts = Localizer.LoadFolder(Shipped, logger);

        foreach (var locale in texts.Locales)
        {
            foreach (var phrase in ListGeneratedPhrases())
                texts.Get(locale, phrase);
        }

        Assert.Empty(logger.Errors);
    }

    [Fact]
    public void Every_generated_text_fills_all_its_placeholders()
    {
        var unfilled = ListGeneratedPhrases()
            .Select(phrase => Texts.Get("en", phrase))
            .Where(text => text.Contains('{'))
            .ToList();

        Assert.Empty(unfilled);
    }

    [Theory]
    [InlineData("en", 1, "one")]
    [InlineData("en", 0, "other")]
    [InlineData("en", 2, "other")]
    [InlineData("pl", 1, "one")]
    [InlineData("pl", 3, "few")]
    [InlineData("pl", 12, "many")]
    [InlineData("pl", 22, "few")]
    [InlineData("pl", 25, "many")]
    public void Plural_categories_follow_the_language(string locale, int count, string expected) =>
        Assert.Equal(expected, PluralRules.PickCategory(locale, count));

    [Fact]
    public void English_fractions_are_other() => Assert.Equal("other", PluralRules.PickCategory("en", 1.5m));

    [Fact]
    public void A_key_outside_the_group_of_its_file_is_refused()
    {
        var root = Directory.CreateTempSubdirectory("locales").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "en"));
            File.WriteAllText(Path.Combine(root, "en", "chat.json"), """{ "chat.ok": "Fine" }""");
            Assert.Equal("Fine", Localizer.LoadFolder(root, new ListLogger()).Get("en", new Phrase("chat.ok")));

            File.WriteAllText(Path.Combine(root, "en", "chat.json"), """{ "economy.ok": "Fine" }""");
            Assert.Throws<FormatException>(() => Localizer.LoadFolder(root, new ListLogger()));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Texts_without_english_are_refused()
    {
        var root = Directory.CreateTempSubdirectory("locales").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "pl"));
            File.WriteAllText(Path.Combine(root, "pl", "chat.json"), """{ "chat.ok": "Dobrze" }""");
            Assert.Throws<InvalidOperationException>(() => Localizer.LoadFolder(root, new ListLogger()));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void A_second_language_is_offered_after_English_and_speaks_its_own_texts()
    {
        var root = Directory.CreateTempSubdirectory("locales").FullName;
        try
        {
            foreach (var (locale, text) in new[] { ("pl", "Dobrze"), ("en", "Fine") })
            {
                Directory.CreateDirectory(Path.Combine(root, locale));
                File.WriteAllText(Path.Combine(root, locale, "chat.json"), $$"""{ "chat.ok": "{{text}}" }""");
            }

            var texts = Localizer.LoadFolder(root, new ListLogger());
            Assert.Equal(["en", "pl"], texts.Locales);
            Assert.True(texts.HasLocale("pl"));
            Assert.Equal("Dobrze", texts.Get("pl", new Phrase("chat.ok")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Every_language_of_the_repository_reaches_the_server()
    {
        var folders = Directory.EnumerateDirectories(Path.Combine(Repository.Root, "locales"))
            .Where(folder => Directory.EnumerateFiles(folder, "*.json").Any(file => !Path.GetFileName(file).StartsWith("ui.", StringComparison.Ordinal)))
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal);

        Assert.Equal(folders, Texts.Locales.Order(StringComparer.Ordinal));
        Assert.Equal("en", Texts.Locales[0]);
    }

    // Whole counts only: gold, minutes and players come whole.
    [Fact]
    public void Every_plural_text_has_every_form_its_language_picks()
    {
        var missing = new List<string>();
        foreach (var folder in Directory.EnumerateDirectories(Path.Combine(Repository.Root, "locales")))
        {
            var locale = Path.GetFileName(folder);
            var needed = Enumerable.Range(0, PluralSample).Select(n => PluralRules.PickCategory(locale, n)).ToHashSet(StringComparer.Ordinal);
            foreach (var file in Directory.EnumerateFiles(folder, "*.json"))
            {
                foreach (var property in JsonDocument.Parse(File.ReadAllText(file)).RootElement.EnumerateObject())
                {
                    if (property.Value.ValueKind != JsonValueKind.Object)
                        continue;

                    var forms = property.Value.EnumerateObject().Select(form => form.Name).ToHashSet(StringComparer.Ordinal);
                    missing.AddRange(needed.Where(form => !forms.Contains(form)).Select(form => $"{locale}: {property.Name} {form}"));
                }
            }
        }

        Assert.Empty(missing);
    }

    // The keys of the server's own texts are written only through the generated classes, so the compiler checks them.
    [Fact]
    public void No_text_key_is_written_as_a_string_in_the_code()
    {
        var keys = Directory.EnumerateFiles(Path.Combine(Repository.Root, "locales", Localizer.BaseLocale), "*.json")
            .Where(file => !Path.GetFileName(file).StartsWith("ui.", StringComparison.Ordinal))
            .SelectMany(file => JsonDocument.Parse(File.ReadAllText(file)).RootElement.EnumerateObject().Select(property => property.Name))
            .ToHashSet(StringComparer.Ordinal);

        var found = SourceFiles.In("src", "*.cs")
            .SelectMany(file => Literal.Matches(File.ReadAllText(file)).Where(m => keys.Contains(m.Groups[1].Value))
                .Select(m => $"{Path.GetFileName(file)}: {m.Groups[1].Value}"))
            .ToList();

        Assert.NotEmpty(keys);
        Assert.Empty(found);
    }

    [GeneratedRegex(@"""([a-z_]+(?:\.[a-z0-9_]+)+)""")]
    private static partial Regex Literal { get; }

    // "x" for whatever a placeholder takes.
    private static IEnumerable<Phrase> ListGeneratedPhrases()
    {
        var classes = typeof(ChatText).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: true, IsSealed: true } && t.Namespace == typeof(ChatText).Namespace && t.Name.EndsWith("Text"));

        foreach (var type in classes)
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Static).Where(p => p.PropertyType == typeof(Phrase)))
                yield return (Phrase)property.GetValue(null)!;

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(m => m.ReturnType == typeof(Phrase)))
                yield return (Phrase)method.Invoke(null, method.GetParameters().Select(_ => (object)"x").ToArray())!;
        }
    }
}
