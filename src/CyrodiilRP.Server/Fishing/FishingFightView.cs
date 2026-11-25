namespace CyrodiilRP.Server.Fishing;

// The fight as the page shows it: the line's tension (red from TensionWarn, 1 snaps), the fish's stamina, how far it
// is, what it does, the rod's lean and the reel as the server took them, how near a slack line is to losing the fish,
// and how heavy it feels.
internal sealed record FishingFightView(
    double Tension,
    double TensionWarn,
    double Stamina,
    int DistanceCm,
    FishPull Pull,
    int Lean,
    bool Reeling,
    double Slack,
    FightFeel Feel);
