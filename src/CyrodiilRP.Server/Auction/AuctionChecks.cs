using CyrodiilRP.Data.Auction;
using CyrodiilRP.Server.Exchange;
using CyrodiilRP.Server.Localization;

namespace CyrodiilRP.Server.Auction;

// What the house refuses before anything moves. Null when the request may go on.
internal static class AuctionChecks
{
    private const string HoursSeparator = ", ";

    // Item: the stack the request names among what the character carries, null when it is not there.
    public static Phrase? CheckPost(AuctionPostRequest request, ExchangeItem? item, int gold, int openLots, AuctionSettings settings, int maxPrice)
    {
        if (openLots >= settings.MaxLots)
            return AuctionText.TooMany(settings.MaxLots);

        if (item is null)
            return AuctionText.NoItem;

        if (request.Count < 1 || request.Count > item.Count)
            return AuctionText.Count(item.Count);

        if (CheckPrices(request.StartPrice, request.BuyoutPrice, maxPrice) is { } wrong)
            return wrong;

        if (!settings.DurationHours.Contains(request.Hours))
            return AuctionText.Hours(string.Join(HoursSeparator, settings.DurationHours));

        var fee = FindFee(request, settings);
        return gold < fee ? AuctionText.FeePoor(fee, gold) : null;
    }

    public static int FindFee(AuctionPostRequest request, AuctionSettings settings) =>
        AuctionRules.Fee(Math.Max(request.StartPrice, request.BuyoutPrice), settings.ListingFeePercent);

    // A price of 0 is none, and a lot asks at least one.
    public static Phrase? CheckPrices(int startPrice, int buyoutPrice, int maxPrice)
    {
        if (startPrice is < 0 || startPrice > maxPrice || buyoutPrice is < 0 || buyoutPrice > maxPrice)
            return AuctionText.Price(maxPrice);

        if (startPrice == 0 && buyoutPrice == 0)
            return AuctionText.NoPrice;

        return startPrice > 0 && buyoutPrice > 0 && buyoutPrice <= startPrice ? AuctionText.BuyoutLow : null;
    }

    // A bid or a purchase, before its gold leaves the purse.
    public static Phrase? CheckPayment(int amount, int gold, int maxPrice)
    {
        if (amount < 1 || amount > maxPrice)
            return AuctionText.Price(maxPrice);

        return gold < amount ? AuctionText.Poor(amount, gold) : null;
    }
}
