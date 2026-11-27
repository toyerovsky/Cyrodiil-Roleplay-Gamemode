namespace CyrodiilRP.Server.Fishing;

// What a far cast brings: the farther from the angler the float lands, the bigger the fish and the likelier a rare one.
internal static class FarCastRules
{
    // Centimetres from the angler: a cast no farther than NearAt is a near one, one at FarAt or beyond a far one.
    public const float NearAt = 300f;
    public const float FarAt = 2000f;

    // The skill's size skew is multiplied by this: above 1 favours small fish. About 1 at the plain rod's reach.
    public const double SkewNear = 1.4;
    public const double SkewFar = 0.8;

    // A rare species' weight against the others is multiplied by this. About 1 at the plain rod's reach.
    public const double RareNear = 0.3;
    public const double RareFar = 1.4;

    // 0 for a near cast to 1 for a far one.
    public static double FarShare(float distanceCm)
    {
        if (!float.IsFinite(distanceCm) || distanceCm < 0)
            throw new ArgumentOutOfRangeException(nameof(distanceCm), distanceCm, "A cast lands some way out.");

        return Math.Clamp((distanceCm - NearAt) / (FarAt - NearAt), 0, 1);
    }

    public static double SizeSkew(float distanceCm) => SkewNear + (SkewFar - SkewNear) * FarShare(distanceCm);

    public static double RareWeight(float distanceCm) => RareNear + (RareFar - RareNear) * FarShare(distanceCm);
}
