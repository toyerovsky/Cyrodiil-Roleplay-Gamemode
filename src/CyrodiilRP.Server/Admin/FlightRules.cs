using System.Numerics;

namespace CyrodiilRP.Server.Admin;

// /afly flies by teleport, as the platform cannot switch a character's collision off: the server keeps putting the
// flyer on along the way they walk, at the height they hold.
internal static class FlightRules
{
    // Centimetres a second the server carries a flyer, on top of their own walking where nothing stops it.
    public const float Speed = 600f;

    // A gap smaller than this, in centimetres, is not worth a teleport.
    public const float Slack = 1f;

    // A push shorter than this is a flyer standing still.
    private const float Idle = 0.05f;

    public static readonly TimeSpan MoveEvery = TimeSpan.FromMilliseconds(50);

    // Where a flyer is put this time. Push: the way they walk, as the game reports it, whose height is left out.
    public static Vector3 FindNext(Vector3 current, Vector3 push, float height)
    {
        var flat = new Vector2(push.X, push.Y);
        var step = flat.Length() < Idle ? Vector2.Zero : Vector2.Normalize(flat) * Speed * (float)MoveEvery.TotalSeconds;
        return new Vector3(current.X + step.X, current.Y + step.Y, height);
    }
}
