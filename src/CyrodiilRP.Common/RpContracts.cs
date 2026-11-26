using CyrodiilRP.Common.Chat;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Common;

[RpcContracts]
public static partial class RpContracts
{
    // Speech, or a command when it starts with a slash.
    [ClientToServer]
    public static partial void SubmitMessage(string text);

    // Whether the chat's entry holds text, said when that changes, for the mark over the player's name.
    [ClientToServer]
    public static partial void ReportTyping(bool typing);

    [ServerToClient]
    public static partial void DeliverMessage(ChatLine line);

    // A short notice on screen, outside the chat log.
    [ServerToClient]
    public static partial void Notify(string text);

    // An action (/me) over the head of the player who did it, by their number (the raw value of their PlayerId), for this
    // long.
    [ServerToClient]
    public static partial void Overhead(int speaker, string text, int showMs);

    [ServerToClient]
    public static partial void Locale(string locale);

    [ServerToClient]
    public static partial void StartNaming();

    [ClientToServer]
    public static partial void SubmitName(string firstName, string lastName);

    // Empty when the name was accepted, otherwise why not, in the player's language.
    [ServerToClient]
    public static partial void NameAnswer(string problem);

    // Opens the game's own character creator: race, sex, face and hair.
    [ServerToClient]
    public static partial void StartAppearance();

    [ClientToServer]
    public static partial void FinishAppearance();

    [ClientToServer]
    public static partial void OpenSelfWheel();

    // The server picks whom: the nearest player or notice board in front.
    [ClientToServer]
    public static partial void OpenInteractionWheel();

    // The character spoke the mode's topic (TalkTopic) to one of the mode's people, named by the ref id the server gave
    // them, and the game's dialogue has closed. The server opens that person's window.
    [ClientToServer]
    public static partial void TalkTo(uint npc);

    // A WheelView as JSON, for the page.
    [ServerToClient]
    public static partial void ShowWheel(string json);

    [ClientToServer]
    public static partial void PickWheel(string id);

    [ClientToServer]
    public static partial void CloseWheel();

    // Opens the chat with the start of a command, for an option that needs more from the player.
    [ServerToClient]
    public static partial void PrefillChat(string text);

    // An ExchangeView as JSON, for the page.
    [ServerToClient]
    public static partial void ExchangeState(string json);

    [ServerToClient]
    public static partial void ExchangeRefused(string text);

    [ServerToClient]
    public static partial void ExchangeDone(string text);

    [ServerToClient]
    public static partial void ExchangeClosed();

    // The gold of the offer, which replaces the amount before.
    [ClientToServer]
    public static partial void ExchangeOffer(int gold);

    // How many of a stack the offer holds, 0 for none, by the key the page got for it.
    [ClientToServer]
    public static partial void ExchangeOfferItem(string stack, int count);

    // Counts only for the version its player saw.
    [ClientToServer]
    public static partial void ExchangeConfirm(int version);

    [ClientToServer]
    public static partial void ExchangeCancel();

    // A PartyView as JSON, for the panel, at every change.
    [ServerToClient]
    public static partial void PartyState(string json);

    // An AsksView as JSON: the invitations to a party and the trades asked of the player or by them. Answerable: the
    // one Y and N answer, 0 for none.
    [ServerToClient]
    public static partial void AsksState(string json, int answerable);

    // Yes or no to an ask, by the id the player saw.
    [ClientToServer]
    public static partial void AnswerAsk(int id, bool accepted);

    // A TraderView as JSON: opens the trader's window or brings it up to date.
    [ServerToClient]
    public static partial void TraderState(string json);

    // What a buy came to, in the player's language: bought or refused.
    [ServerToClient]
    public static partial void TraderAnswer(string text, bool bought);

    [ServerToClient]
    public static partial void TraderClosed();

    // The label over the mode's traders, in the player's language.
    [ServerToClient]
    public static partial void TraderLabel(string text);

    [ClientToServer]
    public static partial void TraderBuy(string ware, int count);

    [ClientToServer]
    public static partial void TraderClose();

    // A ForemanView as JSON: opens a Jobs foreman's window or brings it up to date.
    [ServerToClient]
    public static partial void ForemanState(string json);

    // What a deal came to: done or refused. The Jobs mod's own sentence, in English.
    [ServerToClient]
    public static partial void ForemanAnswer(string text, bool done);

    [ServerToClient]
    public static partial void ForemanClosed();

    // A deal from the window: "pay" for the wages owed, or an offer's id and how many.
    [ClientToServer]
    public static partial void ForemanDeal(string offer, int count);

    [ClientToServer]
    public static partial void ForemanClose();

    // The label over the mode's bankers, in the player's language.
    [ServerToClient]
    public static partial void BankerLabel(string text);

    // A BankView as JSON: opens the bank's window or brings it up to date.
    [ServerToClient]
    public static partial void BankState(string json);

    // What a deposit or a withdrawal came to, in the player's language: done or refused.
    [ServerToClient]
    public static partial void BankAnswer(string text, bool done);

    [ServerToClient]
    public static partial void BankClosed();

    [ClientToServer]
    public static partial void BankDeposit(int amount);

    [ClientToServer]
    public static partial void BankWithdraw(int amount);

    [ClientToServer]
    public static partial void BankClose();

    // The label over the mode's auctioneers, in the player's language.
    [ServerToClient]
    public static partial void AuctioneerLabel(string text);

    // An AuctionView as JSON, as the window opens at an auctioneer.
    [ServerToClient]
    public static partial void AuctionOpened(string json);

    // An AuctionView as JSON for a window already open. One in flight as the player closes the window opens nothing.
    [ServerToClient]
    public static partial void AuctionState(string json);

    // What a request came to, in the player's language: done or refused.
    [ServerToClient]
    public static partial void AuctionAnswer(string text, bool done);

    [ServerToClient]
    public static partial void AuctionClosed();

    // Category: an AuctionCategory by name. Page: from 0.
    [ClientToServer]
    public static partial void AuctionSearch(string text, string category, int page);

    // Item: the stack's key in the window. A price of 0 is none.
    [ClientToServer]
    public static partial void AuctionPost(string item, int count, int startPrice, int buyoutPrice, int hours);

    [ClientToServer]
    public static partial void AuctionBid(long lot, int amount);

    // Price: the one the window showed, so nobody pays a price they did not see.
    [ClientToServer]
    public static partial void AuctionBuy(long lot, int price);

    [ClientToServer]
    public static partial void AuctionWithdraw(long lot);

    // Parcel: 0 for every one that waits.
    [ClientToServer]
    public static partial void AuctionCollect(long parcel);

    [ClientToServer]
    public static partial void AuctionClose();

    // The label over the mode's stablemasters, in the player's language.
    [ServerToClient]
    public static partial void StablemasterLabel(string text);

    // A StableView as JSON: opens the stable's window or brings it up to date.
    [ServerToClient]
    public static partial void StableState(string json);

    // What a purchase came to, in the player's language: bought or refused.
    [ServerToClient]
    public static partial void StableAnswer(string text, bool bought);

    [ServerToClient]
    public static partial void StableClosed();

    // One of the horses the window offered, by the name it came under.
    [ClientToServer]
    public static partial void StableBuy(string horse);

    // The labels over the mode's warrior, mage and support mentors and the registrar, in the player's language.
    [ServerToClient]
    public static partial void MentorLabels(string warrior, string mage, string support, string registrar);

    // A MentorView as JSON: opens a mentor's window or brings it up to date.
    [ServerToClient]
    public static partial void MentorState(string json);

    // What a choice came to, in the player's language: chosen or refused.
    [ServerToClient]
    public static partial void MentorAnswer(string text, bool chosen);

    [ServerToClient]
    public static partial void MentorClosed();

    // One of the specializations the window offered, by the name it came under.
    [ClientToServer]
    public static partial void MentorChoose(string specialization);

    [ClientToServer]
    public static partial void MentorClose();

    [ClientToServer]
    public static partial void StableClose();

    // The menu key: the server answers with the menu on its first tab.
    [ClientToServer]
    public static partial void OpenMenu();

    // A MenuView as JSON: opens the player's menu or brings it up to date.
    [ServerToClient]
    public static partial void MenuState(string json);

    // One of the languages the menu offered: the server answers with Locale and the menu again.
    [ClientToServer]
    public static partial void ChooseLanguage(string locale);

    // An AdminView as JSON: opens the staff's panel or brings it up to date.
    [ServerToClient]
    public static partial void AdminState(string json);

    // What the panel asked for (AdminParts), as JSON.
    [ServerToClient]
    public static partial void AdminPart(string kind, string json);

    // A command the panel ran answered, in the staff member's language: done or refused.
    [ServerToClient]
    public static partial void AdminAnswer(string text, bool done);

    // A staff command, one of those the panel has buttons for, run as if typed.
    [ClientToServer]
    public static partial void AdminRun(string line);

    // One of AdminParts, with what it needs: a player's number, an AdminAuditFilter as JSON or the text to look for.
    [ClientToServer]
    public static partial void AdminAsk(string kind, string argument);

    [ClientToServer]
    public static partial void AdminClose();

    // The staff member no longer holds the panel's permission (/aroles).
    [ServerToClient]
    public static partial void AdminClosed();

    // The staff froze the character: the reason, in the player's language.
    [ServerToClient]
    public static partial void Frozen(string notice);

    [ServerToClient]
    public static partial void Thawed();

    // Only the player's own game can bring its character back.
    [ServerToClient]
    public static partial void Revive();

    // A WelcomeView as JSON: what greets a player who has just joined.
    [ServerToClient]
    public static partial void Welcome(string json);

    // The player took their character over: the welcome goes.
    [ServerToClient]
    public static partial void WelcomeClosed();

    [ClientToServer]
    public static partial void PlayCharacter();

    // The server answers with StartNaming, to a player without a character only.
    [ClientToServer]
    public static partial void NewCharacter();

    // The dice board's key (Home): the server answers with DiceState.
    [ClientToServer]
    public static partial void OpenDice();

    // A DiceView as JSON: opens the dice board or brings it up to date.
    [ServerToClient]
    public static partial void DiceState(string json);

    // The board was closed on the player's side.
    [ClientToServer]
    public static partial void CloseDice();

    // /board with the board open: the server closes it.
    [ServerToClient]
    public static partial void CloseDiceBoard();

    // Dice the board picked, counted in its order: d6, d10, d20, d100. The request id comes back in the answer.
    [ClientToServer]
    public static partial void ThrowDice(int requestId, int d6, int d10, int d20, int d100);

    // The server's dice for the request, in the order d6s, d10s, d20s, d100s.
    [ServerToClient]
    public static partial void DiceThrown(int requestId, int[] values);

    // No roll for the request, why not in the player's language.
    [ServerToClient]
    public static partial void DiceRefused(int requestId, string text);

    // /roll with the board open: these dice into the tray and thrown. All zero: the tray as it is.
    [ServerToClient]
    public static partial void ThrowOnBoard(int d6, int d10, int d20, int d100);

    // A roll the server already made for something else (a trait of the lore card): the dice landing on these values, in a
    // small board that takes no input, or on the board if it is open. The reason in the player's language.
    [ServerToClient]
    public static partial void PresentDice(string reason, int d6, int d10, int d20, int d100, int[] values);

    // One of the dice sets the board and the menu offer. FromMenu: the menu asked, so it is the menu that comes back.
    [ClientToServer]
    public static partial void ChooseDiceSet(string set, bool fromMenu);

    // The lore card's keys: End (look false) opens the player's own card, Shift+End (look true) the card of the player in
    // front. The server answers with LoreAnswer, request id 0.
    [ClientToServer]
    public static partial void OpenLore(bool look);

    // A request of the lore card's page, JSON with an "op". The answer comes back under the same id, above 0.
    [ClientToServer]
    public static partial void LoreAsk(int requestId, string json);

    // The answer to a request, or with id 0 the server's own: {"op":"edit"} opens the editor, {"op":"view",...} a card.
    // The page opens on the server's own.
    [ServerToClient]
    public static partial void LoreAnswer(int requestId, string json);

    // The lore card was closed on the player's side.
    [ClientToServer]
    public static partial void CloseLore();

    // /mylore with the card open: the server closes it.
    [ServerToClient]
    public static partial void CloseLoreCard();

    // The names over the other players' heads, as this player sees them: JSON from a player's number to the name. A
    // player not in it shows no name.
    [ServerToClient]
    public static partial void KnownNames(string json);

    // The menu's lore tab: whether everyone sees the character's name. The server answers with the menu on that tab.
    [ClientToServer]
    public static partial void ChooseNamePublic(bool namePublic);

    // A /faction command a button of the menu's faction tab runs, as if typed.
    [ClientToServer]
    public static partial void FactionRun(string line);

    // What it came to, in the player's language, and the menu (a MenuView as JSON) as it is after it.
    [ServerToClient]
    public static partial void FactionAnswer(string text, bool done, string menu);

    // The notice boards standing in the player's area, every one of them: the game's asset each stands as, then four numbers
    // a board, x, y and z in centimetres and the yaw in degrees.
    [ServerToClient]
    public static partial void NoticeBoardsHere(string asset, float[] spots);

    // The game dropped what it showed with its last pawn: the server answers with NoticeBoardsHere.
    [ClientToServer]
    public static partial void AskNoticeBoards();

    // A BoardView as JSON: opens the board's window or brings it up to date.
    [ServerToClient]
    public static partial void BoardState(string json);

    // What pinning or taking down a notice came to, in the player's language: done or refused.
    [ServerToClient]
    public static partial void BoardAnswer(string text, bool done);

    [ServerToClient]
    public static partial void BoardClosed();

    [ClientToServer]
    public static partial void BoardPost(string text);

    // One of the player's own notices, by the id the window showed.
    [ClientToServer]
    public static partial void BoardTakeDown(long notice);

    [ClientToServer]
    public static partial void BoardClose();

    [ClientToServer]
    public static partial void GamblingOpen(int opponent, bool rules);

    [ClientToServer]
    public static partial void GamblingClose();

    [ClientToServer]
    public static partial void GamblingInvite(int opponent, int stake, string mode);

    [ClientToServer]
    public static partial void GamblingAct(long game, int version, string action, int amount);

    [ServerToClient]
    public static partial void GamblingState(int kind, string json);

    // A GuideView as JSON: a guide's card over the game, at the first use of a window, the first entry or read again.
    [ServerToClient]
    public static partial void Guide(string json);

    // E or Enter on the card: what waited behind it goes on.
    [ClientToServer]
    public static partial void GuideContinue();

    // Esc on the card, or (replaced) another window took its place, and then nothing follows the card.
    [ClientToServer]
    public static partial void GuideClose(bool replaced);

    // A guide read again from the menu's Help tab, by its topic's name.
    [ClientToServer]
    public static partial void ReadGuide(string topic);

    // The player has done nothing for a while: this many seconds before [AFK] shows over their head.
    [ServerToClient]
    public static partial void AwayWarning(int secondsLeft);

    // [AFK] shows over the player's head: this many seconds since they last did anything.
    [ServerToClient]
    public static partial void AwayMarked(int secondsAway);

    // The player is back, or the warning is over.
    [ServerToClient]
    public static partial void AwayEnded();

    // A key or the mouse in one of the mode's pages: the player is at the keyboard while a window holds them still.
    [ClientToServer]
    public static partial void ReportPresence();

    // Fishing from the wheel: the server asks the player's game where the water is around the character.
    [ServerToClient]
    public static partial void FishingLook();

    // What the look found (a FishingVerdict's name): for water, the way the character faces it, in degrees, and the
    // height of its surface, in centimetres.
    [ClientToServer]
    public static partial void FishingSpot(string verdict, float yaw, float waterZ);

    // The line is in and the player may cast: at the start of the fishing and after every catch or loss. The character
    // is held facing the water at this yaw, its surface this high.
    [ServerToClient]
    public static partial void FishingAim(float yaw, float waterZ);

    // A FishingView as JSON, for the fishing page: what to show of the line now.
    [ServerToClient]
    public static partial void FishingPage(string json);

    // A fish took the bait: the strike's moment counts from when the player's game heard this.
    [ServerToClient]
    public static partial void FishingBite();

    [ServerToClient]
    public static partial void FishingEnded();

    // The float lands where the player aimed, on the water: its point, in centimetres.
    [ClientToServer]
    public static partial void FishingCast(float x, float y, float z);

    // The cast key went down or up. A strike carries how many milliseconds after FishingBite the player's game heard it.
    [ClientToServer]
    public static partial void FishingSpace(bool down, int reactionMs);

    [ClientToServer]
    public static partial void FishingStop();

    // To everyone near an angler (by their PlayerId's raw value), the angler included: they hold a rod now, or not.
    [ServerToClient]
    public static partial void AnglerShown(int angler, bool fishing);

    // To everyone near: an angler's float lies here, in centimetres.
    [ServerToClient]
    public static partial void FishingFloat(int angler, float x, float y, float z);

    [ServerToClient]
    public static partial void FishingFloatGone(int angler);

    // To everyone near: the water splashes here (a FishingSplashKind's name), seen and heard in the world.
    [ServerToClient]
    public static partial void FishingSplash(string kind, float x, float y, float z);

    // The plots of the player's area: the asset their signs stand as, each plot's id, then ten numbers a plot: its first
    // tile x and y, width and depth in tiles, ground and height in centimetres, and its sign's x, y, z and yaw.
    [ServerToClient]
    public static partial void PlotsHere(string signAsset, long[] plots, float[] numbers);

    // The pieces standing on the plots of the player's area: each one's id and asset, then seven numbers a piece: x, y and
    // z in centimetres, pitch, yaw and roll in degrees, and its scale, and each one's game object (its ref id, 0 for a mesh
    // the player's game stands itself).
    [ServerToClient]
    public static partial void PiecesHere(long[] pieces, string[] assets, float[] numbers, long[] references);

    // The game dropped what it showed with its last pawn: the server answers with PlotsHere and PiecesHere.
    [ClientToServer]
    public static partial void AskBuilding();

    // A PlotSaleView as JSON: opens a plot's sign window or brings it up to date.
    [ServerToClient]
    public static partial void PlotSaleState(string json);

    // What buying a plot came to, in the player's language, and whether it was bought.
    [ServerToClient]
    public static partial void PlotSaleAnswer(string text, bool bought);

    [ServerToClient]
    public static partial void PlotSaleClosed();

    // Buy the plot whose sign window is open.
    [ClientToServer]
    public static partial void PlotSaleBuy();

    [ClientToServer]
    public static partial void PlotSaleClose();

    // The building catalogue as the architect's game needs it, sent as the mode opens: each piece's key, asset and scale.
    [ServerToClient]
    public static partial void ArchitectCatalogue(string[] keys, string[] assets, float[] scales);

    // The drawer's pieces as a JSON array of ArchitectPieceView, sent as the mode opens in parts under a message's limit:
    // the first part starts the list again, the others add to it.
    [ServerToClient]
    public static partial void ArchitectPieces(string json, bool first);

    // An ArchitectView as JSON: opens architect mode or brings it up to date. Plot: the plot it builds on, 0 while the
    // staff choose one or mark a new one. Tool: an ArchitectTool by name.
    [ServerToClient]
    public static partial void ArchitectState(string json, long plot, string tool);

    // What a change in architect mode came to, in the player's language: done or refused.
    [ServerToClient]
    public static partial void ArchitectAnswer(string text, bool done);

    [ServerToClient]
    public static partial void ArchitectClosed();

    // A plot taken away with every piece on it, by its id, once the staff member said so on the page.
    [ClientToServer]
    public static partial void ArchitectRemovePlot(long plot);

    // A piece of the catalogue, by its key, put down at x, y, z and the yaw, then its pitch and roll when it stands tilted.
    [ClientToServer]
    public static partial void ArchitectPlace(string piece, float[] spot);

    // A piece on the plot, by its id, moved to x, y, z and the yaw.
    [ClientToServer]
    public static partial void ArchitectMove(long piece, float[] spot);

    [ClientToServer]
    public static partial void ArchitectRemove(long piece);

    // A piece's box as the game measured its mesh at its scale: min x, y, z, then max x, y, z, in centimetres.
    [ClientToServer]
    public static partial void ArchitectMeasured(string piece, float[] bounds);

    // A new plot the staff marked: its first tile x and y, width and depth, then its ground height and its sign's x, y, z
    // and yaw, and the window's form as JSON (a PlotForm).
    [ClientToServer]
    public static partial void ArchitectMarkPlot(int[] tiles, float[] ground, string form);

    // A piece on the plot taken apart into its parts: each part's catalogue key, and six numbers a part: x, y and z in
    // centimetres, pitch, yaw and roll in degrees.
    [ClientToServer]
    public static partial void ArchitectBreakApart(long piece, string[] parts, float[] spots);

    // The sign of the plot being built on, moved to x, y, z and the yaw: the plot's front turned.
    [ClientToServer]
    public static partial void ArchitectMoveSign(float[] sign);

    // A plot to build on, by its id, or 0 to choose another.
    [ClientToServer]
    public static partial void ArchitectChoose(long plot);

    // Marking a new plot, from choosing one.
    [ClientToServer]
    public static partial void ArchitectNewPlot();

    [ClientToServer]
    public static partial void ArchitectClose();
}
