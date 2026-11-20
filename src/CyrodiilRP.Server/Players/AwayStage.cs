namespace CyrodiilRP.Server.Players;

// How far a still player is from [AFK] over their head.
internal enum AwayStage
{
    Here,

    // The screen asks whether the player is there and counts down to the mark: no mark yet.
    Warned,

    Away,
}
