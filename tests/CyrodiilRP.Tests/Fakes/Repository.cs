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
                       .ThenBy(folder => folder.Built)
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

    // "oblivion-server-win64-1.0.0-rc.2", "oblivion-server-linux64-v1.1-rp-framework-688fc210": a version's release
    // comes after its other builds, and those go by when the server was built, as a commit's hash has no order.
    private sealed record ServerFolder(string Path, Version Version, bool Release, DateTime Built)
    {
        private const char VersionMark = 'v';

        private static readonly string[] Binaries = ["server", "server.exe"];

        public static ServerFolder Parse(string path)
        {
            var binary = Binaries.Select(name => System.IO.Path.Combine(path, name)).FirstOrDefault(File.Exists)
                         ?? throw new InvalidOperationException($"No OblivionMP server in {path} (no server or server.exe).");
            var built = File.GetLastWriteTimeUtc(binary);

            var parts = System.IO.Path.GetFileName(path).Split('-');
            var at = Array.FindIndex(parts, part => Version.TryParse(part.TrimStart(VersionMark), out _));
            if (at < 0)
                return new ServerFolder(path, new Version(), false, built);

            return new ServerFolder(path, Version.Parse(parts[at].TrimStart(VersionMark)), at == parts.Length - 1, built);
        }
    }
}
