namespace CyrodiilRP.Tests.Fakes;

internal static class SourceFiles
{
    // Build output, installed packages and what a tool writes, none of it written by hand.
    private static readonly string[] Skipped = ["bin", "obj", "node_modules", "dist", "test-results", "playwright-report", "TestResults", "Migrations"];

    public static IEnumerable<string> In(string folder, params string[] patterns) =>
        patterns.SelectMany(pattern => Directory.EnumerateFiles(Path.Combine(Repository.Root, folder), pattern, SearchOption.AllDirectories))
            .Where(file => !Path.GetRelativePath(Repository.Root, file).Split(Path.DirectorySeparatorChar).Any(Skipped.Contains));

    public static string Relative(string file) => Path.GetRelativePath(Repository.Root, file).Replace('\\', '/');
}
