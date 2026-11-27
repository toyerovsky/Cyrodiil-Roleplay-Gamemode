using CyrodiilRP.Server.Lore;

namespace CyrodiilRP.Server.Fisher;

// A record holder's name for one reader of the board.
internal static class RecordNames
{
    // ShownInPlay: what ShownNames gives this reader while the holder is in the game, null when they are not. The race
    // of a character away is not known and neither is who was introduced to it, so only a name made public is written
    // then, and everyone else is the stranger of no known race.
    public static string Show(RecordHolder holder, string? shownInPlay) =>
        shownInPlay ?? (holder.NamePublic ? holder.Name : GameRaces.Label(GameRace.Unknown));
}
