using CyrodiilRP.Common;
using CyrodiilRP.Server.Dice;
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

    public event Action<PlayerId, string, string>? NameSubmitted;

    public event Action<PlayerId>? AppearanceFinished;

    public event Action<PlayerId>? SelfWheelAsked;

    public event Action<PlayerId>? InteractionWheelAsked;

    public event Action<PlayerId, string>? WheelPicked;

    public event Action<PlayerId>? WheelClosed;

    public event Action<PlayerId, int>? ExchangeOffered;

    public event Action<PlayerId, string, int>? ExchangeItemOffered;

    public event Action<PlayerId, int>? ExchangeConfirmed;

    public event Action<PlayerId>? ExchangeCancelled;

    public event Action<PlayerId, int, bool>? AskAnswered;

    public event Action<PlayerId, string, int>? TraderBought;

    public event Action<PlayerId>? TraderLeft;

    public event Action<PlayerId, int>? BankDeposited;

    public event Action<PlayerId, int>? BankWithdrawn;

    public event Action<PlayerId>? BankLeft;

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

    partial void OnSubmitName(RpcContext context, string firstName, string lastName) =>
        callbacks.Raise("rpc.submit_name", NameSubmitted, context.Sender, firstName, lastName);

    partial void OnFinishAppearance(RpcContext context) =>
        callbacks.Raise("rpc.finish_appearance", AppearanceFinished, context.Sender);

    partial void OnOpenSelfWheel(RpcContext context) => callbacks.Raise("rpc.open_self_wheel", SelfWheelAsked, context.Sender);

    partial void OnOpenInteractionWheel(RpcContext context) =>
        callbacks.Raise("rpc.open_interaction_wheel", InteractionWheelAsked, context.Sender);

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

    partial void OnBankDeposit(RpcContext context, int amount) => callbacks.Raise("rpc.bank_deposit", BankDeposited, context.Sender, amount);

    partial void OnBankWithdraw(RpcContext context, int amount) => callbacks.Raise("rpc.bank_withdraw", BankWithdrawn, context.Sender, amount);

    partial void OnBankClose(RpcContext context) => callbacks.Raise("rpc.bank_close", BankLeft, context.Sender);

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
}
