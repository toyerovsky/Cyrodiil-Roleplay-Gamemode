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
                       .OfType<ServerFolder>()
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
    // comes after its other builds, and those go by when the server was built, as a commit's hash has no order. A folder
    // without a version is passed over, as pack.ps1 does.
    private sealed record ServerFolder(string Path, Version Version, bool Release, DateTime Built)
    {
        private const char VersionMark = 'v';

        private static readonly string[] Binaries = ["server", "server.exe"];

        public static ServerFolder? Parse(string path)
        {
            var parts = System.IO.Path.GetFileName(path).Split('-');
            var at = Array.FindIndex(parts, part => Version.TryParse(part.TrimStart(VersionMark), out _));
            if (at < 0)
                return null;

            var binary = Binaries.Select(name => System.IO.Path.Combine(path, name)).FirstOrDefault(File.Exists)
                         ?? throw new InvalidOperationException($"No OblivionMP server in {path} (no server or server.exe).");

            // Version gives a missing part -1, which would sort "1.1" below "1.1.0".
            var named = Version.Parse(parts[at].TrimStart(VersionMark));
            var version = new Version(named.Major, named.Minor, Math.Max(named.Build, 0), Math.Max(named.Revision, 0));
            return new ServerFolder(path, version, at == parts.Length - 1, File.GetLastWriteTimeUtc(binary));
        }
    }
}
