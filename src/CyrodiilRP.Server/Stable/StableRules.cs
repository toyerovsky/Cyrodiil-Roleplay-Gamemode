using System.Numerics;
using CyrodiilRP.Data;
using CyrodiilRP.Server.Admin;
using CyrodiilRP.Server.Localization;
using ReadyM.Api.Idents;

namespace CyrodiilRP.Server.Stable;

internal static class StableRules
{
    // Marks the horses the mode stands, as TraderOrigin marks its people, and below them. Only the server reads it.
    public const uint HorseSpawnerRefId = 0xFFFFFFFA;

    // The game's own horses of each coat (HorsePaint, HorseBay, HorseChestnut, HorseWhite, HorseBlack): no script, no
    // owner, the health of the horse its stables sell, and its packages only wander about where it stands.
    public const uint PaintBase = 0x0001F11E;
    public const uint BayBase = 0x0001F11D;
    public const uint ChestnutBase = 0x00015B92;
    public const uint WhiteBase = 0x0001F11B;
    public const uint BlackBase = 0x0001F11A;

    // To the side of the character, so it stands neither in the way ahead nor behind.
    public const float SideCentimetres = 200f;

    // A horse stands taller than a person: made a little above the character's middle, it drops onto the ground rather
    // than into it.
    public const float RaiseCentimetres = 50f;

    // A horse the cell's save brought back after a restart goes within this long of its cell loading.
    public static readonly TimeSpan SweepEvery = TimeSpan.FromSeconds(5);

    private const float DegreesToRadians = MathF.PI / 180f;

    public static uint FindBase(HorseKind horse) => horse switch
    {
        HorseKind.Paint => PaintBase,
        HorseKind.Bay => BayBase,
        HorseKind.Chestnut => ChestnutBase,
        HorseKind.White => WhiteBase,
        HorseKind.Black => BlackBase,
    };

    public static Phrase FindName(HorseKind horse) => horse switch
    {
        HorseKind.Paint => StableText.HorsePaint,
        HorseKind.Bay => StableText.HorseBay,
        HorseKind.Chestnut => StableText.HorseChestnut,
        HorseKind.White => StableText.HorseWhite,
        HorseKind.Black => StableText.HorseBlack,
    };

    // The open world only: an interior has no room for a horse, and the game keeps horses out of its cities.
    public static bool IsOpenWorld(AreaId area) => area == GameSpace.Outside;

    // The yaw is in degrees from +X towards +Y.
    public static Vector3 FindSpotBeside(Vector3 position, float yawDegrees)
    {
        var yaw = yawDegrees * DegreesToRadians;
        return position + new Vector3(-MathF.Sin(yaw) * SideCentimetres, MathF.Cos(yaw) * SideCentimetres, RaiseCentimetres);
    }

    // The name a page sends, back to the horse. False for any other.
    public static bool TryRead(string name, out HorseKind horse)
    {
        foreach (var each in Enum.GetValues<HorseKind>())
        {
            if (each.ToString() == name)
            {
                horse = each;
                return true;
            }
        }

        horse = default;
        return false;
    }
}
