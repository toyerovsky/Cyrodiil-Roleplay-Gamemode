namespace CyrodiilRP.Server;

// When a player who stands still, does not turn and does not write counts as away from the keyboard.
internal sealed class AwaySettings
{
    public int AfterMinutes { get; set; } = 5;

    public TimeSpan After => TimeSpan.FromMinutes(AfterMinutes);

    // A wrong value stops the mod at start.
    public void RequireValid()
    {
        if (AfterMinutes < 1)
            throw new InvalidOperationException($"server/config.json: away.afterMinutes is {AfterMinutes}, below 1.");
    }
}
