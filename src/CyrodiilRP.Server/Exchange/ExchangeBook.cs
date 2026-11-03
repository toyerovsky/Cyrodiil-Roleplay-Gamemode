using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Server.Exchange;

// In memory, an ask under both its players and a deal under both its sides.
internal sealed class ExchangeBook
{
    private readonly Dictionary<OnlinePlayer, PlayerAsk> _asks = [];
    private readonly Dictionary<OnlinePlayer, ExchangeDeal> _deals = [];

    public bool HasDeals => _deals.Count > 0;

    public IReadOnlyList<ExchangeDeal> Deals => _deals.Values.Distinct().ToList();

    public bool IsTrading(OnlinePlayer player) => _deals.ContainsKey(player);

    public bool HasAsk(OnlinePlayer player) => _asks.ContainsKey(player);

    public bool IsAskedBy(OnlinePlayer player, OnlinePlayer asker) =>
        _asks.TryGetValue(player, out var ask) && ask.Asked == player && ask.Asker == asker;

    public bool IsCurrent(PlayerAsk ask) => _asks.TryGetValue(ask.Asker, out var current) && current == ask;

    public PlayerAsk? FindAsk(OnlinePlayer player) => _asks.GetValueOrDefault(player);

    public ExchangeDeal? FindDeal(OnlinePlayer player) => _deals.GetValueOrDefault(player);

    public PlayerAsk Ask(OnlinePlayer asker, OnlinePlayer asked)
    {
        var ask = new PlayerAsk(asker, asked);
        _asks[asker] = ask;
        _asks[asked] = ask;
        return ask;
    }

    public void Drop(PlayerAsk ask)
    {
        ask.Timeout?.Cancel();
        _asks.Remove(ask.Asker);
        _asks.Remove(ask.Asked);
    }

    public ExchangeDeal Open(OnlinePlayer asker, OnlinePlayer asked, long nowMs)
    {
        var deal = new ExchangeDeal(asker, asked, nowMs);
        _deals[asker] = deal;
        _deals[asked] = deal;
        return deal;
    }

    public void Close(ExchangeDeal deal)
    {
        foreach (var side in deal.Sides)
            _deals.Remove(side.Player);
    }
}
