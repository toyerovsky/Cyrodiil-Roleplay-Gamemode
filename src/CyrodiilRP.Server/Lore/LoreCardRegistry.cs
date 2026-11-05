namespace CyrodiilRP.Server.Lore;

// The cards of the characters in play, by character id, and those on their way or that could not be read.
internal sealed class LoreCardRegistry
{
    private readonly Dictionary<long, LoreCard> _cards = [];

    private readonly Dictionary<long, LoreCardState> _pending = [];

    public LoreCardState Find(long characterId, out LoreCard card)
    {
        if (_cards.TryGetValue(characterId, out var found))
        {
            card = found;
            return LoreCardState.Ready;
        }

        card = LoreCard.Blank;
        return _pending.GetValueOrDefault(characterId, LoreCardState.Ready);
    }

    // A character made in this session has nothing stored, so its card starts blank.
    public LoreCard FindOrStart(long characterId)
    {
        if (!_cards.TryGetValue(characterId, out var card))
            _cards[characterId] = card = LoreCard.Blank;

        return card;
    }

    public void BeginLoading(long characterId) => _pending[characterId] = LoreCardState.Loading;

    public void MarkUnreadable(long characterId) => _pending[characterId] = LoreCardState.Unreadable;

    public bool IsLoading(long characterId) => _pending.GetValueOrDefault(characterId) == LoreCardState.Loading;

    public void Put(long characterId, LoreCard card)
    {
        _pending.Remove(characterId);
        _cards[characterId] = card;
    }

    public void Remove(long characterId)
    {
        _pending.Remove(characterId);
        _cards.Remove(characterId);
    }
}
