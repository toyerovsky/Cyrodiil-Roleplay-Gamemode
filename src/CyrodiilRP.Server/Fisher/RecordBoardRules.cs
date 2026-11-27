namespace CyrodiilRP.Server.Fisher;

internal static class RecordBoardRules
{
    // The fish of a species the board lists.
    public const int TopCount = 5;

    // How long one reading of the database serves everyone who asks for the board.
    public static readonly TimeSpan KeptFor = TimeSpan.FromMinutes(1);

    // A player's game is answered no more often than this.
    public static readonly TimeSpan AskSoonest = TimeSpan.FromSeconds(1);
}
