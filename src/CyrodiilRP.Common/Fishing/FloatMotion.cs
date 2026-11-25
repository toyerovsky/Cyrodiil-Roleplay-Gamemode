namespace CyrodiilRP.Common.Fishing;

// How a float lying on the water moves up and down: a gentle bob, and a short dip when a fish nibbles. Centimetres and
// seconds.
public static class FloatMotion
{
    public const float BobHeight = 1.5f;
    public const float BobSeconds = 1.8f;

    // A smaller, quicker wave over the bob, so it does not look like a machine.
    public const float RippleShare = 0.3f;
    public const float RippleSeconds = 0.7f;

    public const float DipDepth = 5f;
    public const float DipSeconds = 0.45f;

    public static float Bob(float seconds) =>
        BobHeight * (MathF.Sin(MathF.Tau * seconds / BobSeconds) + RippleShare * MathF.Sin(MathF.Tau * seconds / RippleSeconds));

    // Down and back up within the dip's time after a nibble, nothing before or after it.
    public static float Dip(float sinceNibble) =>
        sinceNibble is >= 0f and <= DipSeconds ? -DipDepth * MathF.Sin(MathF.PI * sinceNibble / DipSeconds) : 0f;
}
