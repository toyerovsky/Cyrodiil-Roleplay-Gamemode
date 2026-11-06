namespace Roleplay.Jobs.Server;

/// <summary>Every player's hits on one tree or node, in the order they first struck, and when the last one landed. A hit
/// can count for more than one (a power attack's damage on a tree).</summary>
public sealed class HitTally<TPlayer> where TPlayer : notnull
{
    private readonly List<(TPlayer Player, int Hits)> _byPlayer = [];

    public IReadOnlyList<(TPlayer Player, int Hits)> ByPlayer => _byPlayer;

    public double LastHitAt { get; private set; }

    public int Hits => _byPlayer.Sum(entry => entry.Hits);

    public void Add(TPlayer player, double at, int amount = 1)
    {
        LastHitAt = at;
        var index = _byPlayer.FindIndex(entry => EqualityComparer<TPlayer>.Default.Equals(entry.Player, player));
        if (index < 0)
            _byPlayer.Add((player, amount));
        else
            _byPlayer[index] = (player, _byPlayer[index].Hits + amount);
    }

    public override string ToString() => string.Join(", ", _byPlayer.Select(entry => $"{entry.Player}: {entry.Hits}"));
}
