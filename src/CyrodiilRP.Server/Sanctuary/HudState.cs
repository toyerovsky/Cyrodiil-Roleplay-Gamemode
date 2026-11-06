namespace CyrodiilRP.Server.Sanctuary;

// What a player's badge last showed: inside the Sanctuary, an aggressor there, and when the streak ends on the
// guardian's clock. A change of any of them is sent.
internal readonly record struct HudState(bool Inside, bool Aggressor, double Until)
{
    public static readonly HudState Hidden = new(false, false, double.NegativeInfinity);
}
