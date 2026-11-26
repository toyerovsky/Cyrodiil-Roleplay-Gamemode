using System.Text;
using System.Text.Json;

namespace CyrodiilRP.Server.Building;

// The drawer's pieces as JSON arrays of ArchitectPieceView, in parts a message carries: a string in an RPC is at most
// 65,535 bytes, and the whole catalogue with its kits is more.
internal static class ArchitectPieceParts
{
    // Half a message's string, so a part never comes near the limit.
    public const int MostBytes = 32_000;

    private const int Brackets = 2;
    private const int Comma = 1;

    public static List<string> Write(IReadOnlyList<ArchitectPieceView> pieces, int mostBytes)
    {
        var parts = new List<string>();
        var part = new List<string>();
        var bytes = Brackets;
        foreach (var piece in pieces)
        {
            var json = JsonSerializer.Serialize(piece, ArchitectJson.Default.ArchitectPieceView);
            var size = Encoding.UTF8.GetByteCount(json) + Comma;
            if (part.Count > 0 && bytes + size > mostBytes)
            {
                parts.Add(Join(part));
                part.Clear();
                bytes = Brackets;
            }

            part.Add(json);
            bytes += size;
        }

        if (part.Count > 0 || parts.Count == 0)
            parts.Add(Join(part));

        return parts;
    }

    private static string Join(List<string> part) => "[" + string.Join(',', part) + "]";
}
