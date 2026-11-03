using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace CyrodiilRP.Texts;

// A class per group (ChatText from chat.json) with a member per key: a property for a text without {placeholders}, a
// method taking them for one with. A locale that differs from English is a warning, which the build treats as an error.
[Generator]
public sealed class TextsGenerator : IIncrementalGenerator
{
    private const string BaseLocale = "en";
    private const string LocalesFolder = "locales";
    private const string Namespace = "CyrodiilRP.Server.Localization";
    private const string ClassSuffix = "Text";
    private const string Category = "CyrodiilRP.Texts";

    private static readonly Regex PlaceholderPattern = new Regex(@"\{([A-Za-z_][A-Za-z0-9_]*)\}");

    private static readonly DiagnosticDescriptor Unreadable = DefineRule("CRT001", "A locale file cannot be read", "{0}: {1}", DiagnosticSeverity.Error);
    private static readonly DiagnosticDescriptor OutsideGroup = DefineRule("CRT002", "A key is outside its file's group", "'{0}' does not start with '{1}.'", DiagnosticSeverity.Error);
    private static readonly DiagnosticDescriptor Untranslated = DefineRule("CRT003", "A text is missing in a locale", "'{0}' is missing in {1}", DiagnosticSeverity.Warning);
    private static readonly DiagnosticDescriptor Unknown = DefineRule("CRT004", "A locale has a key English does not", "'{0}' of {1} is not in English", DiagnosticSeverity.Warning);
    private static readonly DiagnosticDescriptor OtherPlaceholders = DefineRule("CRT005", "A translation has other placeholders", "'{0}' in {1} has {2}, English has {3}", DiagnosticSeverity.Warning);
    private static readonly DiagnosticDescriptor SameName = DefineRule("CRT006", "Two keys give one member", "'{0}' and '{1}' both become {2}", DiagnosticSeverity.Error);
    private static readonly DiagnosticDescriptor Twice = DefineRule("CRT007", "A key is written twice", "'{0}' is written twice", DiagnosticSeverity.Error);
    private static readonly DiagnosticDescriptor NoBase = DefineRule("CRT008", "The English texts are missing", "locales/en has no files", DiagnosticSeverity.Error);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var files = context.AdditionalTextsProvider
            .Where(file => IsLocaleFile(file.Path))
            .Select((file, ct) => new LocaleFile(file.Path, file.GetText(ct)?.ToString() ?? ""))
            .Collect();
        context.RegisterSourceOutput(files, Emit);
    }

    private static bool IsLocaleFile(string path) =>
        path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
        && string.Equals(Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(path))), LocalesFolder, StringComparison.OrdinalIgnoreCase);

    private static void Emit(SourceProductionContext context, ImmutableArray<LocaleFile> files)
    {
        var locales = new Dictionary<string, Dictionary<string, Located>>(StringComparer.Ordinal);
        var groups = new Dictionary<string, List<Located>>(StringComparer.Ordinal);
        foreach (var file in files.OrderBy(f => f.Path, StringComparer.Ordinal))
        {
            var locale = Path.GetFileName(Path.GetDirectoryName(file.Path));
            var group = Path.GetFileNameWithoutExtension(file.Path);
            List<LocaleEntry> entries;
            try
            {
                entries = LocaleJson.Parse(file.Text);
            }
            catch (LocaleFormatException e)
            {
                context.ReportDiagnostic(Diagnostic.Create(Unreadable, Locate(file.Path, e.Line), file.Path, e.Message));
                continue;
            }
            if (!locales.TryGetValue(locale, out var keys)) locales[locale] = keys = new Dictionary<string, Located>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                var located = new Located(entry, file.Path);
                if (!entry.Key.StartsWith(group + ".", StringComparison.Ordinal))
                    context.ReportDiagnostic(Diagnostic.Create(OutsideGroup, Locate(file.Path, entry.Line), entry.Key, group));
                else if (keys.ContainsKey(entry.Key))
                    context.ReportDiagnostic(Diagnostic.Create(Twice, Locate(file.Path, entry.Line), entry.Key));
                else
                {
                    keys[entry.Key] = located;
                    if (locale == BaseLocale)
                    {
                        if (!groups.TryGetValue(group, out var list)) groups[group] = list = new List<Located>();
                        list.Add(located);
                    }
                }
            }
        }

        if (!locales.TryGetValue(BaseLocale, out var basis))
        {
            context.ReportDiagnostic(Diagnostic.Create(NoBase, Location.None));
            return;
        }
        foreach (var pair in locales.Where(p => p.Key != BaseLocale).OrderBy(p => p.Key, StringComparer.Ordinal)) Compare(context, basis, pair.Key, pair.Value);

        foreach (var pair in groups.OrderBy(g => g.Key, StringComparer.Ordinal))
            context.AddSource(ToClassName(pair.Key) + ".g.cs", SourceText.From(WriteClass(context, pair.Key, pair.Value), Encoding.UTF8));
    }

    private static void Compare(SourceProductionContext context, Dictionary<string, Located> basis, string locale, Dictionary<string, Located> keys)
    {
        foreach (var pair in basis.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            if (!keys.TryGetValue(pair.Key, out var translated))
            {
                context.ReportDiagnostic(Diagnostic.Create(Untranslated, Locate(pair.Value.Path, pair.Value.Entry.Line), pair.Key, locale));
                continue;
            }
            var expected = FindPlaceholders(pair.Value.Entry);
            var found = FindPlaceholders(translated.Entry);
            if (!new HashSet<string>(expected).SetEquals(found))
                context.ReportDiagnostic(Diagnostic.Create(OtherPlaceholders, Locate(translated.Path, translated.Entry.Line), pair.Key, locale, FormatPlaceholders(found), FormatPlaceholders(expected)));
        }
        foreach (var pair in keys.Where(p => !basis.ContainsKey(p.Key)).OrderBy(p => p.Key, StringComparer.Ordinal))
            context.ReportDiagnostic(Diagnostic.Create(Unknown, Locate(pair.Value.Path, pair.Value.Entry.Line), pair.Key, locale));
    }

    private static string WriteClass(SourceProductionContext context, string group, List<Located> keys)
    {
        var name = ToClassName(group);
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated/> by CyrodiilRP.Texts from locales/en/" + group + ".json.");
        builder.AppendLine("#nullable enable");
        builder.AppendLine("namespace " + Namespace + ";");
        builder.AppendLine();
        builder.AppendLine("internal static partial class " + name);
        builder.AppendLine("{");
        var members = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var located in keys.OrderBy(k => k.Entry.Key, StringComparer.Ordinal))
        {
            var key = located.Entry.Key;
            var member = ToMemberName(key.Substring(group.Length + 1));
            if (members.TryGetValue(member, out var other))
            {
                context.ReportDiagnostic(Diagnostic.Create(SameName, Locate(located.Path, located.Entry.Line), other, key, name + "." + member));
                continue;
            }
            members[member] = key;
            builder.AppendLine("    /// <summary>" + EscapeXml(located.Entry.Forms[0]) + "</summary>");
            var placeholders = FindPlaceholders(located.Entry);
            if (placeholders.Count == 0)
            {
                builder.AppendLine("    public static Phrase " + member + " { get; } = new(\"" + key + "\");");
                continue;
            }
            var parameters = string.Join(", ", placeholders.Select(p => "object? " + ToIdentifier(p)));
            var args = string.Join(", ", placeholders.Select(p => "new TextArg(\"" + p + "\", " + ToIdentifier(p) + ")"));
            builder.AppendLine("    public static Phrase " + member + "(" + parameters + ") => new(\"" + key + "\", " + args + ");");
        }
        builder.AppendLine("}");
        return builder.ToString();
    }

    // In order of first appearance, which is the parameter order.
    private static List<string> FindPlaceholders(LocaleEntry entry)
    {
        var found = new List<string>();
        foreach (var form in entry.Forms)
            foreach (Match match in PlaceholderPattern.Matches(form))
                if (!found.Contains(match.Groups[1].Value)) found.Add(match.Groups[1].Value);
        return found;
    }

    // "chat" becomes ChatText.
    private static string ToClassName(string group) => ToMemberName(group) + ClassSuffix;

    // "name_length" becomes NameLength. A name starting with a digit gets an underscore.
    private static string ToMemberName(string path)
    {
        var builder = new StringBuilder();
        foreach (var part in path.Split('.', '_', '-'))
            if (part.Length > 0) builder.Append(char.ToUpperInvariant(part[0])).Append(part.Substring(1));
        var name = builder.ToString();
        return name.Length > 0 && char.IsDigit(name[0]) ? "_" + name : name;
    }

    private static string ToIdentifier(string placeholder) =>
        SyntaxFacts.GetKeywordKind(placeholder) != SyntaxKind.None
            ? "@" + placeholder
            : placeholder;

    private static string FormatPlaceholders(IEnumerable<string> placeholders)
    {
        var names = placeholders.Select(p => "{" + p + "}").ToList();
        return names.Count == 0 ? "none" : string.Join(" ", names);
    }

    private static string EscapeXml(string text) => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\n", " ");

    private static Location Locate(string path, int line) =>
        Location.Create(path, new TextSpan(0, 0), new LinePositionSpan(new LinePosition(line, 0), new LinePosition(line, 0)));

    private static DiagnosticDescriptor DefineRule(string id, string title, string message, DiagnosticSeverity severity) =>
        new DiagnosticDescriptor(id, title, message, Category, severity, isEnabledByDefault: true);

    // Equal by value, so an unchanged file does not run the generator again.
    private sealed record LocaleFile(string Path, string Text);

    private sealed record Located(LocaleEntry Entry, string Path);
}
