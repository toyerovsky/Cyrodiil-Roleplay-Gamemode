using System.Numerics;

namespace CyrodiilRP.Common.Building;

// The game's light forms name their light: CandleFat01Yellow256 is yellow and reaches 256 of the game's units. A piece of
// one of them stands a point light where its flame is.
public static class GameLights
{
    public const string FormsPath = "/Game/Forms/worldobjects/light/";

    // How far a light reaches when its name does not say, in the game's units.
    private const int UsualReach = 256;

    // The least number at the end of a name that is a reach, in the game's units.
    private const int LeastReach = 64;

    // How bright a light is for every unit it reaches.
    private const float CandelasPerUnit = 1f / 8f;

    // How far under the top of a standing light's mesh its flame is, as a share of the mesh's height.
    private const float FlameBelowTop = 0.15f;

    private static readonly LightColour Yellow = new("Yellow", 255, 205, 140);

    // Longer names first, so DarkOrange is not read as Orange.
    private static readonly LightColour[] Colours =
    [
        new("DarkOrange", 230, 120, 50),
        new("DarkRed", 180, 50, 40),
        new("Orange", 255, 160, 85),
        new("Yellow", 255, 205, 140),
        new("Red", 255, 90, 60),
        new("Blue", 120, 160, 255),
    ];

    private static readonly string[] HangingWords = ["Chandelier", "Hanging"];

    // False for an asset that is no game light form.
    public static bool TryRead(string asset, out GameLight light)
    {
        light = null!;
        if (!asset.StartsWith(FormsPath, StringComparison.Ordinal))
            return false;

        var name = NameOf(asset);
        var colour = Colours.FirstOrDefault(each => name.Contains(each.Word, StringComparison.Ordinal)) ?? Yellow;
        var units = ReadReach(name);
        var hang = HangingWords.Any(word => name.Contains(word, StringComparison.Ordinal)) ? LightHang.Hanging : LightHang.Standing;
        light = new GameLight(colour.Red, colour.Green, colour.Blue, units * BuildingGrid.CentimetresPerUnit, units * CandelasPerUnit, hang);
        return true;
    }

    // Where the flame of a light standing at the spot, turned by yaw, is: its mesh's box measured at its origin.
    public static Vector3 FlameOf(GameLight light, Vector3 position, float yaw, Vector3 boxMin, Vector3 boxMax)
    {
        var middle = (boxMin + boxMax) / 2f;
        var height = light.Hang switch
        {
            LightHang.Standing => boxMax.Z - (boxMax.Z - boxMin.Z) * FlameBelowTop,
            LightHang.Hanging => middle.Z,
            _ => throw new ArgumentOutOfRangeException(nameof(light), light.Hang, "Not where a light hangs."),
        };
        var turned = Vector3.Transform(new Vector3(middle.X, middle.Y, 0f), Quaternion.CreateFromAxisAngle(Vector3.UnitZ, yaw * MathF.PI / 180f));
        return position + new Vector3(turned.X, turned.Y, height);
    }

    private static string NameOf(string asset)
    {
        var start = asset.LastIndexOf('/') + 1;
        var dot = asset.IndexOf('.', start);
        return dot < 0 ? asset[start..] : asset[start..dot];
    }

    // The number its name ends in, before a word such as Flicker: 256 in CandleFat01Yellow256. A small one is the form's
    // own number (LightsIronLampHangingLong01), not its reach.
    private static int ReadReach(string name)
    {
        var end = name.Length;
        while (end > 0 && char.IsLetter(name[end - 1]))
            end--;
        var start = end;
        while (start > 0 && char.IsAsciiDigit(name[start - 1]))
            start--;

        return int.TryParse(name[start..end], out var units) && units >= LeastReach ? units : UsualReach;
    }

    private sealed record LightColour(string Word, byte Red, byte Green, byte Blue);
}
