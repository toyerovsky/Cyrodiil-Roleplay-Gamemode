namespace Roleplay.Jobs.Server;

/// <summary>
/// What a foreman offers, from their role and what the player carries. Pure, so the server and the tests agree. Offer
/// ids: "buy:" and the index in Sells, "sell:" and the name of what is bought (its entries in Buys taken together, such
/// as both kinds of Stone Brick); <see cref="PayId"/> collects the wages owed.
/// </summary>
public static class ForemanDeals
{
    public const string PayId = "pay";
    public const string BuyPrefix = "buy:";
    public const string SellPrefix = "sell:";

    // At most this many pieces in one deal, as the gamemode's shops
    public const int MostPieces = 20;

    /// <param name="carried">How many of each thing the role buys the player carries, by its name.</param>
    public static IReadOnlyList<ForemanOffer> OffersFor(ForemanRole role, IReadOnlyDictionary<string, int> carried)
    {
        var offers = new List<ForemanOffer>();
        for (var index = 0; index < role.Sells.Count; index++)
        {
            var ware = role.Sells[index];
            offers.Add(new ForemanOffer(BuyPrefix + index, ForemanOfferKind.Buy, ware.Name, ware.Icon, ware.Price, 0));
        }
        foreach (var group in role.Buys.GroupBy(ware => ware.Name))
        {
            var first = group.First();
            offers.Add(new ForemanOffer(SellPrefix + group.Key, ForemanOfferKind.Sell, group.Key, first.Icon, first.Price,
                carried.GetValueOrDefault(group.Key)));
        }
        return offers;
    }

    public static string Pieces(int count, string name) => count == 1 ? $"1 {name}" : $"{count} {name}";
}
