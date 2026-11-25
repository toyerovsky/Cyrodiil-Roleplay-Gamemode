using CyrodiilRP.Common;
using System.Numerics;

using CyrodiilRP.Server.Building;
using CyrodiilRP.Server.Dice;
using CyrodiilRP.Server.Fishing;
using CyrodiilRP.Server.Infrastructure;
using ReadyM.Api.Idents;
using ReadyM.Relay.Server.Sdk.Rpc;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Network;

// Raises what arrives, so this class knows nothing of chat or characters.
[RpcHandlersFor(typeof(RpContracts))]
internal sealed partial class RpcHandlers(Callbacks callbacks)
{
    public event Action<PlayerId, string>? MessageSubmitted;

    public event Action<PlayerId, bool>? TypingReported;

    public event Action<PlayerId, string, string>? NameSubmitted;

    public event Action<PlayerId>? AppearanceFinished;

    public event Action<PlayerId>? SelfWheelAsked;

    public event Action<PlayerId>? InteractionWheelAsked;

    public event Action<PlayerId, uint>? TalkedTo;

    public event Action<PlayerId, string>? WheelPicked;

    public event Action<PlayerId>? WheelClosed;

    public event Action<PlayerId, int>? ExchangeOffered;

    public event Action<PlayerId, string, int>? ExchangeItemOffered;

    public event Action<PlayerId, int>? ExchangeConfirmed;

    public event Action<PlayerId>? ExchangeCancelled;

    public event Action<PlayerId, int, bool>? AskAnswered;

    public event Action<PlayerId, string, int>? TraderBought;

    public event Action<PlayerId>? TraderLeft;

    public event Action<PlayerId, string, int>? ForemanDealt;

    public event Action<PlayerId>? ForemanLeft;

    public event Action<PlayerId, int>? BankDeposited;

    public event Action<PlayerId, int>? BankWithdrawn;

    public event Action<PlayerId>? BankLeft;

    public event Action<PlayerId, Auction.AuctionSearchRequest>? AuctionSearched;

    public event Action<PlayerId, Auction.AuctionPostRequest>? AuctionPosted;

    public event Action<PlayerId, long, int>? AuctionBidPlaced;

    public event Action<PlayerId, long, int>? AuctionBought;

    public event Action<PlayerId, long>? AuctionWithdrawn;

    public event Action<PlayerId, long>? AuctionCollected;

    public event Action<PlayerId>? AuctionLeft;

    public event Action<PlayerId, string>? StableBought;

    public event Action<PlayerId>? StableLeft;

    public event Action<PlayerId, string>? MentorChosen;

    public event Action<PlayerId>? MentorLeft;

    public event Action<PlayerId>? MenuAsked;

    public event Action<PlayerId, string>? LanguageChosen;

    public event Action<PlayerId, string>? AdminRan;

    public event Action<PlayerId, string, string>? AdminAsked;

    public event Action<PlayerId>? AdminClosed;

    public event Action<PlayerId>? CharacterPlayed;

    public event Action<PlayerId>? NewCharacterAsked;

    public event Action<PlayerId>? DiceAsked;

    public event Action<PlayerId>? DiceLeft;

    public event Action<PlayerId, DiceThrow>? DiceThrowAsked;

    public event Action<PlayerId, string, bool>? DiceSetChosen;

    public event Action<PlayerId, bool>? LoreOpenAsked;

    public event Action<PlayerId, int, string>? LoreAsked;

    public event Action<PlayerId>? LoreLeft;

    public event Action<PlayerId, bool>? NamePublicChosen;

    public event Action<PlayerId, string>? FactionRan;

    public event Action<PlayerId>? NoticeBoardsAsked;

    public event Action<PlayerId, string>? BoardPosted;

    public event Action<PlayerId, long>? BoardTakenDown;

    public event Action<PlayerId>? BoardLeft;

    partial void OnSubmitMessage(RpcContext context, string text) =>
        callbacks.Raise("rpc.submit_message", MessageSubmitted, context.Sender, text);

    partial void OnReportTyping(RpcContext context, bool typing) =>
        callbacks.Raise("rpc.report_typing", TypingReported, context.Sender, typing);

    partial void OnSubmitName(RpcContext context, string firstName, string lastName) =>
        callbacks.Raise("rpc.submit_name", NameSubmitted, context.Sender, firstName, lastName);

    partial void OnFinishAppearance(RpcContext context) =>
        callbacks.Raise("rpc.finish_appearance", AppearanceFinished, context.Sender);

    partial void OnOpenSelfWheel(RpcContext context) => callbacks.Raise("rpc.open_self_wheel", SelfWheelAsked, context.Sender);

    partial void OnOpenInteractionWheel(RpcContext context) =>
        callbacks.Raise("rpc.open_interaction_wheel", InteractionWheelAsked, context.Sender);

    partial void OnTalkTo(RpcContext context, uint npc) => callbacks.Raise("rpc.talk_to", TalkedTo, context.Sender, npc);

    partial void OnPickWheel(RpcContext context, string id) => callbacks.Raise("rpc.pick_wheel", WheelPicked, context.Sender, id);

    partial void OnCloseWheel(RpcContext context) => callbacks.Raise("rpc.close_wheel", WheelClosed, context.Sender);

    partial void OnExchangeOffer(RpcContext context, int gold) => callbacks.Raise("rpc.exchange_offer", ExchangeOffered, context.Sender, gold);

    partial void OnExchangeOfferItem(RpcContext context, string stack, int count) =>
        callbacks.Raise("rpc.exchange_offer_item", ExchangeItemOffered, context.Sender, stack, count);

    partial void OnExchangeConfirm(RpcContext context, int version) =>
        callbacks.Raise("rpc.exchange_confirm", ExchangeConfirmed, context.Sender, version);

    partial void OnExchangeCancel(RpcContext context) => callbacks.Raise("rpc.exchange_cancel", ExchangeCancelled, context.Sender);

    partial void OnAnswerAsk(RpcContext context, int id, bool accepted) => callbacks.Raise("rpc.answer_ask", AskAnswered, context.Sender, id, accepted);

    partial void OnTraderBuy(RpcContext context, string ware, int count) => callbacks.Raise("rpc.trader_buy", TraderBought, context.Sender, ware, count);

    partial void OnTraderClose(RpcContext context) => callbacks.Raise("rpc.trader_close", TraderLeft, context.Sender);

    partial void OnForemanDeal(RpcContext context, string offer, int count) => callbacks.Raise("rpc.foreman_deal", ForemanDealt, context.Sender, offer, count);

    partial void OnForemanClose(RpcContext context) => callbacks.Raise("rpc.foreman_close", ForemanLeft, context.Sender);

    partial void OnBankDeposit(RpcContext context, int amount) => callbacks.Raise("rpc.bank_deposit", BankDeposited, context.Sender, amount);

    partial void OnBankWithdraw(RpcContext context, int amount) => callbacks.Raise("rpc.bank_withdraw", BankWithdrawn, context.Sender, amount);

    partial void OnBankClose(RpcContext context) => callbacks.Raise("rpc.bank_close", BankLeft, context.Sender);

    partial void OnAuctionSearch(RpcContext context, string text, string category, int page) =>
        callbacks.Raise("rpc.auction_search", AuctionSearched, context.Sender, new Auction.AuctionSearchRequest(text, category, page));

    partial void OnAuctionPost(RpcContext context, string item, int count, int startPrice, int buyoutPrice, int hours) =>
        callbacks.Raise("rpc.auction_post", AuctionPosted, context.Sender, new Auction.AuctionPostRequest(item, count, startPrice, buyoutPrice, hours));

    partial void OnAuctionBid(RpcContext context, long lot, int amount) => callbacks.Raise("rpc.auction_bid", AuctionBidPlaced, context.Sender, lot, amount);

    partial void OnAuctionBuy(RpcContext context, long lot, int price) => callbacks.Raise("rpc.auction_buy", AuctionBought, context.Sender, lot, price);

    partial void OnAuctionWithdraw(RpcContext context, long lot) => callbacks.Raise("rpc.auction_withdraw", AuctionWithdrawn, context.Sender, lot);

    partial void OnAuctionCollect(RpcContext context, long parcel) => callbacks.Raise("rpc.auction_collect", AuctionCollected, context.Sender, parcel);

    partial void OnAuctionClose(RpcContext context) => callbacks.Raise("rpc.auction_close", AuctionLeft, context.Sender);

    partial void OnStableBuy(RpcContext context, string horse) => callbacks.Raise("rpc.stable_buy", StableBought, context.Sender, horse);

    partial void OnStableClose(RpcContext context) => callbacks.Raise("rpc.stable_close", StableLeft, context.Sender);

    partial void OnMentorChoose(RpcContext context, string specialization) =>
        callbacks.Raise("rpc.mentor_choose", MentorChosen, context.Sender, specialization);

    partial void OnMentorClose(RpcContext context) => callbacks.Raise("rpc.mentor_close", MentorLeft, context.Sender);

    partial void OnOpenMenu(RpcContext context) => callbacks.Raise("rpc.open_menu", MenuAsked, context.Sender);


    partial void OnChooseLanguage(RpcContext context, string locale) =>
        callbacks.Raise("rpc.choose_language", LanguageChosen, context.Sender, locale);

    partial void OnAdminRun(RpcContext context, string line) => callbacks.Raise("rpc.admin_run", AdminRan, context.Sender, line);

    partial void OnAdminAsk(RpcContext context, string kind, string argument) =>
        callbacks.Raise("rpc.admin_ask", AdminAsked, context.Sender, kind, argument);

    partial void OnAdminClose(RpcContext context) => callbacks.Raise("rpc.admin_close", AdminClosed, context.Sender);

    partial void OnPlayCharacter(RpcContext context) => callbacks.Raise("rpc.play_character", CharacterPlayed, context.Sender);

    partial void OnNewCharacter(RpcContext context) => callbacks.Raise("rpc.new_character", NewCharacterAsked, context.Sender);

    partial void OnOpenDice(RpcContext context) => callbacks.Raise("rpc.open_dice", DiceAsked, context.Sender);

    partial void OnCloseDice(RpcContext context) => callbacks.Raise("rpc.close_dice", DiceLeft, context.Sender);

    partial void OnThrowDice(RpcContext context, int requestId, int d6, int d10, int d20, int d100) =>
        callbacks.Raise("rpc.throw_dice", DiceThrowAsked, context.Sender, new DiceThrow(requestId, new DicePick(d6, d10, d20, d100)));

    partial void OnChooseDiceSet(RpcContext context, string set, bool fromMenu) =>
        callbacks.Raise("rpc.choose_dice_set", DiceSetChosen, context.Sender, set, fromMenu);

    partial void OnOpenLore(RpcContext context, bool look) => callbacks.Raise("rpc.open_lore", LoreOpenAsked, context.Sender, look);

    partial void OnLoreAsk(RpcContext context, int requestId, string json) =>
        callbacks.Raise("rpc.lore_ask", LoreAsked, context.Sender, requestId, json);

    partial void OnCloseLore(RpcContext context) => callbacks.Raise("rpc.close_lore", LoreLeft, context.Sender);

    partial void OnChooseNamePublic(RpcContext context, bool namePublic) =>
        callbacks.Raise("rpc.choose_name_public", NamePublicChosen, context.Sender, namePublic);

    partial void OnFactionRun(RpcContext context, string line) => callbacks.Raise("rpc.faction_run", FactionRan, context.Sender, line);

    partial void OnAskNoticeBoards(RpcContext context) => callbacks.Raise("rpc.ask_notice_boards", NoticeBoardsAsked, context.Sender);

    partial void OnBoardPost(RpcContext context, string text) => callbacks.Raise("rpc.board_post", BoardPosted, context.Sender, text);

    partial void OnBoardTakeDown(RpcContext context, long notice) => callbacks.Raise("rpc.board_take_down", BoardTakenDown, context.Sender, notice);

    partial void OnBoardClose(RpcContext context) => callbacks.Raise("rpc.board_close", BoardLeft, context.Sender);

    public event Action<PlayerId, int, bool>? GamblingOpened;
    public event Action<PlayerId>? GamblingHidden;
    public event Action<PlayerId, Gambling.GamblingInvitation>? GamblingInvited;
    public event Action<PlayerId, Gambling.GamblingDecision>? GamblingActed;

    partial void OnGamblingOpen(RpcContext context, int opponent, bool rules) => callbacks.Raise("rpc.gambling_open", GamblingOpened, context.Sender, opponent, rules);
    partial void OnGamblingClose(RpcContext context) => callbacks.Raise("rpc.gambling_close", GamblingHidden, context.Sender);
    partial void OnGamblingInvite(RpcContext context, int opponent, int stake, string mode) => callbacks.Raise("rpc.gambling_invite", GamblingInvited, context.Sender, new Gambling.GamblingInvitation(opponent, stake, mode));
    partial void OnGamblingAct(RpcContext context, long game, int version, string action, int amount) =>
        callbacks.Raise("rpc.gambling_act", GamblingActed, context.Sender, new Gambling.GamblingDecision(game, version, action, amount));

    public event Action<PlayerId>? GuideContinued;

    public event Action<PlayerId, bool>? GuideLeft;

    public event Action<PlayerId, string>? GuideAsked;

    public event Action<PlayerId>? PresenceReported;

    public event Action<PlayerId, FishingSpotReport>? FishingSpotReported;

    public event Action<PlayerId, Vector3>? FishingCastMade;

    public event Action<PlayerId, bool, int>? FishingSpacePressed;

    public event Action<PlayerId>? FishingStopAsked;

    partial void OnGuideContinue(RpcContext context) => callbacks.Raise("rpc.guide_continue", GuideContinued, context.Sender);

    partial void OnGuideClose(RpcContext context, bool replaced) => callbacks.Raise("rpc.guide_close", GuideLeft, context.Sender, replaced);

    partial void OnReadGuide(RpcContext context, string topic) => callbacks.Raise("rpc.read_guide", GuideAsked, context.Sender, topic);

    partial void OnReportPresence(RpcContext context) => callbacks.Raise("rpc.report_presence", PresenceReported, context.Sender);

    partial void OnFishingSpot(RpcContext context, string verdict, float yaw, float waterZ) =>
        callbacks.Raise("rpc.fishing_spot", FishingSpotReported, context.Sender, new FishingSpotReport(verdict, yaw, waterZ));

    partial void OnFishingCast(RpcContext context, float x, float y, float z) =>
        callbacks.Raise("rpc.fishing_cast", FishingCastMade, context.Sender, new Vector3(x, y, z));

    partial void OnFishingSpace(RpcContext context, bool down, int reactionMs) =>
        callbacks.Raise("rpc.fishing_space", FishingSpacePressed, context.Sender, down, reactionMs);

    partial void OnFishingStop(RpcContext context) => callbacks.Raise("rpc.fishing_stop", FishingStopAsked, context.Sender);

    public event Action<PlayerId>? BuildingAsked;

    public event Action<PlayerId, string, float[]>? ArchitectPlaced;

    public event Action<PlayerId, long, float[]>? ArchitectMoved;

    public event Action<PlayerId, long>? ArchitectRemoved;

    public event Action<PlayerId, string, float[]>? ArchitectMeasured;

    public event Action<PlayerId, PlotMarking>? ArchitectMarked;

    public event Action<PlayerId>? ArchitectLeft;

    public event Action<PlayerId, long>? ArchitectChose;

    public event Action<PlayerId>? ArchitectNewPlotAsked;

    partial void OnAskBuilding(RpcContext context) => callbacks.Raise("rpc.ask_building", BuildingAsked, context.Sender);

    partial void OnArchitectPlace(RpcContext context, string piece, float[] spot) =>
        callbacks.Raise("rpc.architect_place", ArchitectPlaced, context.Sender, piece, spot);

    partial void OnArchitectMove(RpcContext context, long piece, float[] spot) =>
        callbacks.Raise("rpc.architect_move", ArchitectMoved, context.Sender, piece, spot);

    partial void OnArchitectRemove(RpcContext context, long piece) => callbacks.Raise("rpc.architect_remove", ArchitectRemoved, context.Sender, piece);

    partial void OnArchitectMeasured(RpcContext context, string piece, float[] bounds) =>
        callbacks.Raise("rpc.architect_measured", ArchitectMeasured, context.Sender, piece, bounds);

    partial void OnArchitectMarkPlot(RpcContext context, int[] tiles, float[] ground, string form) =>
        callbacks.Raise("rpc.architect_mark_plot", ArchitectMarked, context.Sender, new PlotMarking(tiles, ground, form));

    partial void OnArchitectClose(RpcContext context) => callbacks.Raise("rpc.architect_close", ArchitectLeft, context.Sender);

    partial void OnArchitectChoose(RpcContext context, long plot) => callbacks.Raise("rpc.architect_choose", ArchitectChose, context.Sender, plot);

    partial void OnArchitectNewPlot(RpcContext context) => callbacks.Raise("rpc.architect_new_plot", ArchitectNewPlotAsked, context.Sender);
}
