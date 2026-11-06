namespace Roleplay.Jobs.Common;

/// <summary>
/// Which foliage meshes are trees that can be felled: a mesh whose name, its path's last part, contains one of
/// <see cref="TreeMeshes"/> and none of <see cref="ExcludedMeshes"/>, ignoring case. The folders are left out: bushes
/// sit in tree folders, and "Street" holds "tree". The server's config sets both lists, and sends them to the client so
/// the two agree on what a tree is.
/// </summary>
public sealed class TreeFilter(IReadOnlyList<string> treeMeshes, IReadOnlyList<string> excludedMeshes)
{
    public const char Separator = '|';

    public IReadOnlyList<string> TreeMeshes { get; } = Clean(treeMeshes);
    public IReadOnlyList<string> ExcludedMeshes { get; } = Clean(excludedMeshes);

    public bool IsTree(string meshPath)
    {
        var name = meshPath[(meshPath.LastIndexOf('/') + 1)..];
        return TreeMeshes.Any(part => name.Contains(part, StringComparison.OrdinalIgnoreCase))
               && !ExcludedMeshes.Any(part => name.Contains(part, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The one part to narrow a foliage search with, or null when there are several to match.</summary>
    public string? SearchHint => TreeMeshes.Count == 1 ? TreeMeshes[0] : null;

    public static string Join(IReadOnlyList<string> parts) => string.Join(Separator, parts);

    public static IReadOnlyList<string> Split(string joined)
        => joined.Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string[] Clean(IReadOnlyList<string> parts)
    {
        if (parts.FirstOrDefault(part => part.Contains(Separator)) is { } bad)
            throw new ArgumentException($"'{bad}' contains '{Separator}', which separates the parts on the wire.", nameof(parts));
        return [.. parts.Select(part => part.Trim()).Where(part => part.Length > 0)];
    }
}
