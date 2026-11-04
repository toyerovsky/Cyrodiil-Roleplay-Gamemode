using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Server.Exchange;

internal sealed class ExchangeSide(OnlinePlayer player)
{
    private const string ItemSeparator = "; ";

    private readonly Dictionary<ExchangeStack, ExchangeItem> _items = [];

    public OnlinePlayer Player { get; } = player;

    public int Gold { get; set; }

    public IReadOnlyList<ExchangeItem> Items => ExchangeHoldings.Sort(_items.Values);

    public int PiecesOffered => _items.Values.Sum(item => item.Count);

    public bool Confirmed { get; set; }

    // What the window last showed them carrying. A change is sent again.
    public ExchangeHoldings? Seen { get; set; }

    public int CountOffered(ExchangeStack stack) => _items.TryGetValue(stack, out var item) ? item.Count : 0;

    // None takes the stack out of the offer.
    public void Offer(ExchangeItem item)
    {
        if (item.Count > 0)
            _items[item.Stack] = item;
        else
            _items.Remove(item.Stack);
    }

    // For the audit, by the game's editor ids. Null when no item is offered.
    public string? DescribeItems() =>
        _items.Count == 0 ? null : string.Join(ItemSeparator, Items.Select(item => $"{item.Item.Id} x{item.Count}"));
}
