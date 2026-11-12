using System.Numerics;
using System.Text.RegularExpressions;
using CyrodiilRP.Server.Lore;

namespace CyrodiilRP.Server.Notices;

internal static partial class NoticeRules
{
    // A step more than the wheel reaches, before the window closes.
    public const int WalkAwayMetres = 5;
    public const float WalkAway = WalkAwayMetres * CentimetresPerMetre;

    // A board stands this far in front of the staff member who stood it, turned to face them.
    public const float Ahead = 100f;
    public const float FacingTurn = 180f;

    public static readonly TimeSpan LoadRetry = TimeSpan.FromSeconds(10);

    // How often a window checks the player is still at the board, and the boards check an area changed under a player.
    public static readonly TimeSpan LookEvery = TimeSpan.FromSeconds(1);

    // An expired notice comes off its board within this, and is never shown after its expiry: the view leaves it out.
    public static readonly TimeSpan ExpireEvery = TimeSpan.FromMinutes(1);

    private const float CentimetresPerMetre = 100f;
    private const float FullTurn = 360f;
    private const string Break = "\n";
    private const string OneBlankLine = "\n\n";

    // Lines as typed, each without control characters or runs of spaces, and at most one empty line between two.
    public static string Clean(string typed)
    {
        var lines = typed.ReplaceLineEndings(Break).Split(Break).Select(line => LoreEditor.Clean(line, multiline: false));
        return BlankLines().Replace(string.Join(Break, lines).Trim(), OneBlankLine);
    }

    public static Vector3 FindSpot(Vector3 standing, float yawDegrees)
    {
        var (sin, cos) = MathF.SinCos(yawDegrees * MathF.PI / (FullTurn / 2f));
        return standing + new Vector3(cos * Ahead, sin * Ahead, 0f);
    }

    public static float FaceBack(float yawDegrees) => ((yawDegrees + FacingTurn) % FullTurn + FullTurn) % FullTurn;

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex BlankLines();
}
