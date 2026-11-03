using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Localization;

// A folder per locale beside the server half and a file per group of keys. The texts generator refuses a build with a
// server text missing in any locale, so a text missing here is a bug: logged once per key and locale, read as its key.
[Service]
internal sealed partial class Localizer
{
    public const string BaseLocale = "en";

    private const string FolderName = "locales";

    private static readonly HashSet<string> DecimalCommaLocales = ["pl"];

    private readonly Dictionary<string, Dictionary<string, Entry>> _texts;
    private readonly ILogger _logger;
    private readonly Lock _reportedLock = new();
    private readonly HashSet<string> _reported = new(StringComparer.Ordinal);

    public Localizer(ILogger logger)
        : this(Path.Combine(Path.GetDirectoryName(typeof(Localizer).Assembly.Location)!, FolderName), logger)
    {
    }

    private Localizer(string folder, ILogger logger)
    {
        _texts = ReadFolder(folder);
        if (!_texts.ContainsKey(BaseLocale))
            throw new InvalidOperationException($"The {BaseLocale} texts are missing in {folder}.");

        _logger = logger;
        Locales = _texts.Keys.OrderBy(locale => locale == BaseLocale ? 0 : 1).ThenBy(locale => locale, StringComparer.Ordinal).ToList();
    }

    // English first, then the others by their codes.
    public IReadOnlyList<string> Locales { get; }

    public static Localizer LoadFolder(string folder, ILogger logger) => new(folder, logger);

    public bool HasLocale(string locale) => _texts.ContainsKey(locale);

    // An account's locale the server does not have reads English.
    public string PickSupported(string locale) => HasLocale(locale) ? locale : BaseLocale;

    public string Get(string locale, Phrase phrase) => Get(locale, phrase.Key, phrase.Args);

    private string Get(string locale, string key, TextArg[] args)
    {
        if (!_texts.TryGetValue(locale, out var map) || !map.TryGetValue(key, out var entry))
            return ReportMissing(locale, key);

        if (entry.Plural is not { } forms)
            return Fill(locale, entry.Text!, args);

        var form = PluralRules.PickCategory(locale, FindCount(args));
        return forms.TryGetValue(form, out var text) ? Fill(locale, text, args) : ReportMissing(locale, key, form);
    }

    private static Dictionary<string, Dictionary<string, Entry>> ReadFolder(string folder)
    {
        var texts = new Dictionary<string, Dictionary<string, Entry>>(StringComparer.Ordinal);
        foreach (var localeFolder in Directory.EnumerateDirectories(folder))
        {
            var map = new Dictionary<string, Entry>(StringComparer.Ordinal);
            foreach (var file in Directory.EnumerateFiles(localeFolder, "*.json"))
                AddGroup(map, Path.GetFileNameWithoutExtension(file), Parse(File.ReadAllText(file, Encoding.UTF8)), file);

            texts[Path.GetFileName(localeFolder)] = map;
        }

        return texts;
    }

    // The player reads the key. For a plural, the log names the form the count picked.
    private string ReportMissing(string locale, string key, string? form = null)
    {
        var missing = form is null ? key : $"{key} ({form})";
        lock (_reportedLock)
        {
            if (_reported.Add($"{locale} {missing}"))
                _logger.LogError("[CyrodiilRP] The text {Text} is missing in {Locale}.", missing, locale);
        }

        return key;
    }

    private static void AddGroup(Dictionary<string, Entry> map, string group, Dictionary<string, Entry> entries, string file)
    {
        var prefix = group + ".";
        foreach (var (key, entry) in entries)
        {
            if (!key.StartsWith(prefix, StringComparison.Ordinal))
                throw new FormatException($"{file}: '{key}' does not start with '{prefix}'.");

            if (!map.TryAdd(key, entry))
                throw new FormatException($"{file}: '{key}' is written twice.");
        }
    }

    private static decimal FindCount(TextArg[] args)
    {
        foreach (var (name, value) in args)
        {
            if (name == "count")
                return value switch
                {
                    int i => i,
                    long l => l,
                    decimal d => d,
                    double d => (decimal)d,
                    float f => (decimal)f,
                    _ => 0,
                };
        }

        return 0;
    }

    // One pass over the template, so a value that itself contains "{name}" (a player's own words) stays as typed.
    private string Fill(string locale, string text, TextArg[] args)
    {
        if (args.Length == 0 || !text.Contains('{'))
            return text;

        return Placeholder.Replace(text, match =>
        {
            foreach (var (name, value) in args)
            {
                if (name == match.Groups[1].Value)
                    return Format(locale, value);
            }

            return match.Value;
        });
    }

    [GeneratedRegex(@"\{([A-Za-z_][A-Za-z0-9_]*)\}")]
    private static partial Regex Placeholder { get; }

    private string Format(string locale, object? value)
    {
        var text = value switch
        {
            null => "",
            Phrase phrase => Get(locale, phrase),
            decimal d => d.ToString("0.##", CultureInfo.InvariantCulture),
            double d => d.ToString("0.##", CultureInfo.InvariantCulture),
            float f => f.ToString("0.##", CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "",
        };
        return value is decimal or double or float && DecimalCommaLocales.Contains(locale) ? text.Replace('.', ',') : text;
    }

    private static Dictionary<string, Entry> Parse(string json)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        var map = new Dictionary<string, Entry>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            map[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.String => new Entry(property.Value.GetString(), null),
                JsonValueKind.Object => new Entry(null, property.Value.EnumerateObject().ToDictionary(f => f.Name, f => f.Value.GetString() ?? "")),
                _ => throw new FormatException($"Locale key '{property.Name}': a string or an object of plural forms expected."),
            };
        }

        return map;
    }

    private sealed record Entry(string? Text, Dictionary<string, string>? Plural);
}
