using CyrodiilRP.Server.Admin;
using ReadyM.Api.Idents;

namespace CyrodiilRP.Server.Spawn;

internal static class SpawnRules
{
    // The platform moves a player only within the area they are in, and new characters begin in the outside world.
    public static bool Allows(AreaId area) => area == GameSpace.Outside;
}
