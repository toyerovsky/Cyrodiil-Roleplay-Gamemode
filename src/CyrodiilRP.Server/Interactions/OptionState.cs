using CyrodiilRP.Server.Localization;

namespace CyrodiilRP.Server.Interactions;

// How a wedge shows: hidden, closed with the reason it says, or open to pick.
internal sealed class OptionState
{
    public static readonly OptionState Hidden = new(isShown: false, refusal: null);

    public static readonly OptionState Open = new(isShown: true, refusal: null);

    private OptionState(bool isShown, Phrase? refusal)
    {
        IsShown = isShown;
        Refusal = refusal;
    }

    public bool IsShown { get; }

    // Null unless it is closed.
    public Phrase? Refusal { get; }

    public bool IsOpen => IsShown && Refusal is null;

    public static OptionState Close(Phrase reason) => new(isShown: true, reason);

    public static OptionState OpenIf(bool open) => open ? Open : Hidden;

    // Closed with the refusal, open without one.
    public static OptionState OpenUnless(Phrase? refusal) => refusal is null ? Open : Close(refusal);
}
