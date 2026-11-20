using CyrodiilRP.Data.Gambling;

namespace CyrodiilRP.Server.Gambling;

internal sealed class GamblingMatch(GamblingMode mode, int stake, int roundsToWin, int maxReplays)
{
    public const int NoWinner = -1;
    public const int First = 0;
    public const int Second = 1;
    public const int AllDiceMask = (1 << PokerHand.DiceCount) - 1;
    private readonly int[][] _hands = [[], []];
    private int[][] _initial = [[], []];
    private readonly bool[] _raised = [false, false];
    private readonly int[] _picks = [0, 0];
    private readonly bool[] _confirmed = [false, false];
    private int _offer;
    private bool _nextRound;
    private bool _nextReplay;
    private int _rollingSeat;
    private int _rerollSeat;

    public GamblingMode Mode { get; } = mode;
    public int Stake { get; } = stake;
    public GamblingPhase Phase { get; private set; } = GamblingPhase.Rolling;
    public int Turn { get; private set; } = NoWinner;
    public int PendingRaise { get; private set; }
    public int Round { get; private set; } = 1;
    public int Replay { get; private set; }
    public int Winner { get; private set; } = NoWinner;
    public int LastWinner { get; private set; } = NoWinner;
    public int[] Wins { get; } = [0, 0];
    public int[] Raises { get; } = [0, 0];
    public int Pot => checked(2 * Stake + Raises.Sum());
    public IReadOnlyList<GamblingRound> Rounds => _rounds;
    private readonly List<GamblingRound> _rounds = [];

    public int[] Hand(int side) => _hands[side].ToArray();
    public bool Confirmed(int side) => _confirmed[side];
    public int Picked(int side) => _picks[side];

    public bool Allows(int side, GamblingAction action) => action switch
    {
        GamblingAction.Pass => Phase == GamblingPhase.Raise && Turn == side,
        GamblingAction.Raise => Phase == GamblingPhase.Raise && Turn == side && !_raised[side],
        GamblingAction.Call => Phase == GamblingPhase.Call && Turn == side,
        GamblingAction.Fold => (Phase == GamblingPhase.Call || Phase == GamblingPhase.Raise) && Turn == side,
        GamblingAction.Pick => Phase == GamblingPhase.Reroll && !_confirmed[side],
        GamblingAction.Select => Phase == GamblingPhase.Reroll && !_confirmed[side],
    };

    public void Roll(Func<int, int[]> rollFirst, Func<int, int[]> rollSecond)
    {
        RollSeat(First, rollFirst);
        RollSeat(Second, rollSecond);
    }

    public void RollSeat(int side, Func<int, int[]> roll)
    {
        if (Phase != GamblingPhase.Rolling || side != _rollingSeat)
            throw new InvalidOperationException("This match is not waiting for a roll.");

        var count = Mode switch { GamblingMode.Poker => PokerHand.DiceCount, GamblingMode.Favor => 2 };
        _hands[side] = roll(count);
        ValidateRoll(_hands[side], count);
        _rollingSeat++;
        if (side == First) return;
        _initial = [_hands[First].ToArray(), _hands[Second].ToArray()];
        var poker = Mode switch { GamblingMode.Poker => true, GamblingMode.Favor => false };
        if (poker)
        {
            _offer = First;
            OfferNext();
        }
        else
            Score(_hands[First].Sum().CompareTo(_hands[Second].Sum()));
    }

    public void Pass(int side)
    {
        Require(side, GamblingAction.Pass);
        _offer++;
        OfferNext();
    }

    // Called only after the server took this amount from the raiser.
    public void Raise(int side, int amount)
    {
        Require(side, GamblingAction.Raise);
        if (amount < 1 || amount > Stake)
            throw new ArgumentOutOfRangeException(nameof(amount));

        Raises[side] = checked(Raises[side] + amount);
        _raised[side] = true;
        PendingRaise = amount;
        Turn = Other(side);
        Phase = GamblingPhase.Call;
    }

    // Called only after the server took the call from the other player.
    public void Call(int side)
    {
        Require(side, GamblingAction.Call);
        Raises[side] = checked(Raises[side] + PendingRaise);
        PendingRaise = 0;
        _offer++;
        OfferNext();
    }

    public void Fold(int side)
    {
        Require(side, GamblingAction.Fold);
        Forfeit(side == First, side == Second);
    }

    public void Pick(int side, int mask)
    {
        Require(side, GamblingAction.Pick);
        if (mask < 0 || mask > AllDiceMask)
            throw new ArgumentOutOfRangeException(nameof(mask));

        _picks[side] = mask;
        _confirmed[side] = true;
    }

    public bool BothPicked => _confirmed.All(confirmed => confirmed);

    public void Reroll(Func<int, int[]> rollFirst, Func<int, int[]> rollSecond)
    {
        RerollSeat(First, rollFirst);
        RerollSeat(Second, rollSecond);
    }

    public void RerollSeat(int side, Func<int, int[]> roll)
    {
        if (Phase != GamblingPhase.Reroll || !BothPicked || side != _rerollSeat)
            throw new InvalidOperationException("Both players must confirm their dice first.");

        Replace(side, roll);
        _rerollSeat++;
        if (side == First) return;
        Score(PokerHand.Evaluate(_hands[First]).CompareTo(PokerHand.Evaluate(_hands[Second])));
    }

    public void Timeout()
    {
        Action act = Phase switch
        {
            GamblingPhase.Raise => () => Pass(Turn),
            GamblingPhase.Call => () => Fold(Turn),
            GamblingPhase.Reroll => () =>
            {
                for (var side = First; side <= Second; side++)
                    if (!_confirmed[side]) Pick(side, 0);
            },
            GamblingPhase.Rolling => throw new InvalidOperationException("A roll has no decision timeout."),
            GamblingPhase.RoundResult => NextRound,
            GamblingPhase.Finished => throw new InvalidOperationException("A finished match has no decision timeout."),
        };
        act();
    }

    // A match already decided stays as it is: its last throw is still shown while a player may walk off.
    public void Forfeit(bool firstGone, bool secondGone)
    {
        if (!firstGone && !secondGone)
            throw new ArgumentException("A forfeit needs a departing player.");
        if (Phase == GamblingPhase.Finished)
            return;

        Winner = firstGone && secondGone ? NoWinner : firstGone ? Second : First;
        Phase = GamblingPhase.Finished;
        Turn = NoWinner;
    }

    public void Cancel()
    {
        Winner = NoWinner;
        Phase = GamblingPhase.Finished;
        Turn = NoWinner;
    }

    public static int Other(int side) => side == First ? Second : First;

    private void OfferNext()
    {
        while (_offer <= Second && _raised[_offer]) _offer++;
        Phase = _offer <= Second ? GamblingPhase.Raise : GamblingPhase.Reroll;
        Turn = _offer <= Second ? _offer : NoWinner;
    }

    private void Replace(int side, Func<int, int[]> roll)
    {
        var indices = Enumerable.Range(0, PokerHand.DiceCount).Where(index => (_picks[side] & (1 << index)) != 0).ToArray();
        if (indices.Length == 0)
            return;

        var values = roll(indices.Length);
        ValidateRoll(values, indices.Length);
        for (var index = 0; index < indices.Length; index++)
            _hands[side][indices[index]] = values[index];
    }

    private void Score(int difference)
    {
        LastWinner = difference == 0 ? NoWinner : difference > 0 ? First : Second;
        _rounds.Add(new GamblingRound(Round, Replay, _initial[First].ToArray(), _initial[Second].ToArray(), Hand(First), Hand(Second), LastWinner));
        if (LastWinner == NoWinner)
        {
            if (Replay >= maxReplays)
            {
                Cancel();
                return;
            }

            _nextReplay = true;
        }
        else
        {
            Wins[LastWinner]++;
            var needed = Mode switch { GamblingMode.Poker => roundsToWin, GamblingMode.Favor => 1 };
            if (Wins[LastWinner] >= needed)
            {
                Winner = LastWinner;
                Phase = GamblingPhase.Finished;
                Turn = NoWinner;
                return;
            }

            _nextRound = true;
        }

        Phase = GamblingPhase.RoundResult;
        Turn = NoWinner;
    }

    private void NextRound()
    {
        if (_nextRound)
        {
            Round++;
            Replay = 0;
            Array.Clear(_raised);
        }
        if (_nextReplay) Replay++;
        _nextRound = false;
        _nextReplay = false;
        Array.Clear(_confirmed);
        Array.Clear(_picks);
        _rollingSeat = First;
        _rerollSeat = First;
        _hands[First] = [];
        _hands[Second] = [];
        Phase = GamblingPhase.Rolling;
    }

    private void Require(int side, GamblingAction action)
    {
        if (side is < First or > Second || !Allows(side, action))
            throw new InvalidOperationException("That decision is not this player's to make.");
    }

    private static void ValidateRoll(int[] dice, int count)
    {
        if (dice.Length != count || dice.Any(value => value is < 1 or > PokerHand.Sides))
            throw new InvalidOperationException("The roll did not return the requested d6.");
    }
}
