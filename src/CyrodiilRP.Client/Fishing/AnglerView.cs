namespace CyrodiilRP.Client.Fishing;

// What the angler's own game reads of the FishingView the server sends the page: the stage for the keys and the camera,
// the fight, the twitches of the bait (while waiting) and the last outcome for the sounds.
internal sealed record AnglerView(LineStage Stage, AnglerFight? Fight, AnglerTwitch? Twitch, AnglerOutcome? Outcome);
