namespace CyrodiilRP.Server.Building;

internal static class BuildingRules
{
    public static readonly TimeSpan LoadRetry = TimeSpan.FromSeconds(30);

    // How often the server looks for players who came into another area, or walked off their plot in architect mode.
    public static readonly TimeSpan LookEvery = TimeSpan.FromSeconds(1);

    // How far from a plot's edge an architect may be to choose it, and walk before the mode closes, in centimetres. The
    // mode's own camera sees a plot from where the character stands.
    public const float WalkAway = 10000f;

    public const int WalkAwayMetres = (int)(WalkAway / 100f);

    // A spot is x, y, z and the yaw.
    public const int SpotLength = 4;

    // A box is min x, y, z, then max x, y, z.
    public const int BoundsLength = 6;

    // A plot's tiles: first x and y, width and depth.
    public const int TilesLength = 4;

    // A plot's ground height, then its sign's x, y, z and yaw.
    public const int GroundLength = 5;

    // The farthest a sign may stand from its plot's edge, in centimetres.
    public const float SignReach = 500f;

    // The deepest a plot may begin below or above where its sign stands, in centimetres.
    public const float GroundReach = 3000f;
}
