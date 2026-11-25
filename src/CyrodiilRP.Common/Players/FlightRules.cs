using System.Numerics;

namespace CyrodiilRP.Common.Players;

// A staff member's flight, moved by the flyer's own game once a frame. Speeds in centimetres a second.
public static class FlightRules
{
    public const float Speed = 1200f;

    public const float HurriedSpeed = 4800f;

    // A hitch, or the first frame after a load, carries a flyer no farther than this.
    public const float LongestStepSeconds = 0.1f;

    // The move input of the four keys, X to the right and Y ahead: opposite keys cancel, and two at once are 1 long.
    public static Vector2 FindPush(bool isAheadHeld, bool isBackHeld, bool isLeftHeld, bool isRightHeld)
    {
        var push = new Vector2(Weigh(isRightHeld) - Weigh(isLeftHeld), Weigh(isAheadHeld) - Weigh(isBackHeld));
        return push == Vector2.Zero ? push : Vector2.Normalize(push);
    }

    // Forward: the way the camera looks, pitch included. Right: to its right, level. Push: the move input, X to the
    // right and Y ahead, at most 1 long.
    public static Vector3 FindStep(Vector3 forward, Vector3 right, Vector2 push, bool isHurried, float seconds)
    {
        var speed = isHurried ? HurriedSpeed : Speed;
        return (forward * push.Y + right * push.X) * (speed * Math.Clamp(seconds, 0f, LongestStepSeconds));
    }

    private static float Weigh(bool isHeld) => isHeld ? 1f : 0f;
}
