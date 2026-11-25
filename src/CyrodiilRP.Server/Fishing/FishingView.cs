namespace CyrodiilRP.Server.Fishing;

// The fishing page: the line's stage, counters the page plays a cue on when they grow, the rod's bend against the safe
// band while landing, and the last catch or loss.
internal sealed record FishingView(
    FishingStage Stage,
    int Nibbles,
    int Bites,
    double Bend,
    bool Lifting,
    double Tired,
    double SafeFrom,
    double SafeTo,
    int StrikeWindowMs,
    FishingOutcomeView? Outcome);
