using Microsoft.Extensions.Logging;

namespace CyrodiilRP.Server.Exchange;

// A trade moves everything or nothing: a move that fails takes back those before it. The deal has just been held
// against what both sides carry, so a failure here is a bug, and one that cannot be taken back loses items.
internal sealed class ExchangeSettlement(Func<ExchangeMove, bool> tryMove, ILogger logger)
{
    public bool TryRun(IReadOnlyList<ExchangeMove> moves)
    {
        var done = new Stack<ExchangeMove>();
        foreach (var move in moves)
        {
            if (tryMove(move))
            {
                done.Push(move);
                continue;
            }

            logger.LogError("[CyrodiilRP] A trade could not move {Count} of {Stack} from {From}.", move.Count, move.Stack.Key, move.From.Ref);
            foreach (var moved in done)
            {
                if (!tryMove(moved.Reversed))
                    logger.LogError("[CyrodiilRP] A failed trade could not give {Count} of {Stack} back to {To}.", moved.Count, moved.Stack.Key, moved.From.Ref);
            }

            return false;
        }

        return true;
    }
}
