namespace CyrodiilRP.Server.Fishing;

// What the angler's last twitch of the bait came to, for the page.
internal enum TwitchResult
{
    // No twitch since the cast.
    None,

    // In calm water: fish come sooner.
    Drew,

    // In water still troubled by the cast or the last twitch: fish come later.
    Troubled,

    // A fish at the float takes the bait at once.
    Provoked,

    // A fish at the float bolts.
    Spooked,
}
