using System.Runtime.CompilerServices;

namespace CyrodiilRP.Tests.Fakes;

internal static class Repository
{
    private const string SolutionFile = "CyrodiilRP.slnx";
    private const string ServerPrefix = "oblivion-server-";
    private const string ServerVariable = "OBLIVIONMP_SERVER";

    public static string Root { get; } = Find();

    // OBLIVIONMP_SERVER, or the oblivion-server-* folder of the newest version in the repository.
    public static string Server
    {
        get
        {
            if (Environment.GetEnvironmentVariable(ServerVariable) is { Length: > 0 } given)
                return given;

            return Directory.GetDirectories(Root, ServerPrefix + "*")
                       .Select(ServerFolder.Parse)
                       .OrderBy(folder => folder.Version)
                       .ThenBy(folder => folder.Release)
                       .ThenBy(folder => folder.SortableLabel, StringComparer.Ordinal)
                       .LastOrDefault()?.Path
                   ?? throw new InvalidOperationException($"No {ServerPrefix}* folder in {Root}: unpack the server there or set {ServerVariable}.");
        }
    }

    // Above the binaries, or above this source file when the binaries lie outside the repository (--artifacts-path).
    private static string Find()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, FindSourceFolder() })
        {
            for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
            {
                if (File.Exists(Path.Combine(dir.FullName, SolutionFile)))
                    return dir.FullName;
            }
        }

        throw new InvalidOperationException($"No {SolutionFile} above {AppContext.BaseDirectory}.");
    }

    private static string FindSourceFolder([CallerFilePath] string path = "") => Path.GetDirectoryName(path) ?? "";

    // "oblivion-server-win64-1.0.0-rc.2": version 1.0.0, label rc.2, which comes before the release of 1.0.0.
    private sealed record ServerFolder(string Path, Version Version, bool Release, string Label)
    {
        private const int NumberWidth = 10;

        // Numbers padded, so rc.10 comes after rc.2.
        public string SortableLabel => System.Text.RegularExpressions.Regex.Replace(Label, @"\d+", m => m.Value.PadLeft(NumberWidth, '0'));

        public static ServerFolder Parse(string path)
        {
            var parts = System.IO.Path.GetFileName(path).Split('-');
            var at = Array.FindIndex(parts, part => Version.TryParse(part, out _));
            if (at < 0)
                return new ServerFolder(path, new Version(), false, "");

            var label = string.Join('-', parts[(at + 1)..]);
            return new ServerFolder(path, Version.Parse(parts[at]), label.Length == 0, label);
        }
    }
}
