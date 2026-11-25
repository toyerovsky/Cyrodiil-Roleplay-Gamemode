namespace CyrodiilRP.Common.Fishing;

// The cast's power while Space is held: up from nothing to full, back down and up again, so holding on is no gain.
public static class FishingCharge
{
    public const float UpSeconds = 1.1f;

    public static float PowerAt(float heldSeconds)
    {
        if (!float.IsFinite(heldSeconds) || heldSeconds < 0f)
            throw new ArgumentOutOfRangeException(nameof(heldSeconds), heldSeconds, "Space is held a number of seconds from 0 up.");

        var phase = heldSeconds / UpSeconds % 2f;
        return phase <= 1f ? phase : 2f - phase;
    }
}
