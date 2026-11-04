using System.Text.RegularExpressions;
using CyrodiilRP.Tests.Fakes;

namespace CyrodiilRP.Tests.Rules;

// The code rules a machine can check.
public sealed partial class CodeRulesTests
{
    private const char EmDash = '\u2014';
    private const string PackScript = "pack.ps1";

    private static readonly string[] TextPatterns =
        ["*.cs", "*.csproj", "*.props", "*.slnx", "*.json", "*.md", "*.ps1", "*.yml", "*.vue", "*.js", "*.mjs", "*.css", "*.html"];

    private static IEnumerable<string> Written() =>
        new[] { "src", "tests", "locales", "ui", "infra", "mod" }
            .SelectMany(folder => SourceFiles.In(folder, TextPatterns))
            .Concat(Directory.EnumerateFiles(Repository.Root, "*.*").Where(file => TextPatterns.Any(p => file.EndsWith(p[1..], StringComparison.Ordinal))));

    [Fact]
    public void No_text_has_an_em_dash()
    {
        Assert.Empty(Written().Where(file => File.ReadAllText(file).Contains(EmDash)).Select(SourceFiles.Relative));
    }

    [Fact]
    public void Every_text_ends_its_lines_with_lf_alone()
    {
        Assert.Empty(Written().Where(file => File.ReadAllText(file).Contains('\r')).Select(SourceFiles.Relative));
    }

    // On Linux a backslash is a plain character, and a program the script calls, as docker, takes the path as it is.
    [Fact]
    public void The_pack_script_joins_paths_with_forward_slashes()
    {
        var joined = JoinedPath().Matches(File.ReadAllText(Path.Combine(Repository.Root, PackScript))).Select(m => m.Value).ToList();

        Assert.NotEmpty(joined);
        Assert.DoesNotContain(joined, path => path.Contains('\\'));
    }

    // So a type is found by its name.
    [Fact]
    public void Every_source_file_holds_the_one_type_it_is_named_after()
    {
        var wrong = new[] { "src", "tests" }
            .SelectMany(folder => SourceFiles.In(folder, "*.cs"))
            .Select(file => new TypesOfFile(file, TopLevelType().Matches(File.ReadAllText(file)).Select(m => m.Groups[1].Value).ToList()))
            .Where(f => f.Types.Count > 1 || (f.Types.Count == 1 && f.Types[0] != Path.GetFileNameWithoutExtension(f.File)))
            .Select(f => $"{SourceFiles.Relative(f.File)}: {string.Join(", ", f.Types)}")
            .ToList();

        Assert.Empty(wrong);
    }

    // At the start of a line, under a file-scoped namespace: a nested type is indented.
    [GeneratedRegex(@"^(?:(?:public|internal|file|sealed|static|abstract|partial|readonly)\s+)*(?:class|record struct|record|struct|interface|enum)\s+(\w+)", RegexOptions.Multiline)]
    private static partial Regex TopLevelType();

    [GeneratedRegex(@"Join-Path\s+\S+\s+['""][^'""]*['""]")]
    private static partial Regex JoinedPath();

    private sealed record TypesOfFile(string File, List<string> Types);
}
