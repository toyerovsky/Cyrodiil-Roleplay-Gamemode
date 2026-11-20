// The window arrives as JSON (AuctionView.cs): what the character carries, the house's terms, a page of the open lots,
// the character's own lots and leading bids, and what waits for them. A request is answered with a line in the player's
// language and whether it went through.
export function createBridge(readym) {
  return {
    onLocale: (handler) => readym.on("Locale", handler),
    onState: (handler) => readym.on("State", (json) => handler(JSON.parse(json))),
    onAnswer: (handler) => readym.on("Answer", handler),
    onClosed: (handler) => readym.on("Closed", handler),
    signalReady: () => readym.sendAuctionReady(),
    search: (text, category, page) => readym.sendAuctionSearch(text, category, page),
    post: (item, count, startPrice, buyoutPrice, hours) => readym.sendAuctionPost(item, count, startPrice, buyoutPrice, hours),
    bid: (lot, amount) => readym.sendAuctionBid(lot, amount),
    buy: (lot, price) => readym.sendAuctionBuy(lot, price),
    withdraw: (lot) => readym.sendAuctionWithdraw(lot),
    collect: (parcel) => readym.sendAuctionCollect(parcel),
    close: () => readym.sendAuctionClose(),
  };
}
