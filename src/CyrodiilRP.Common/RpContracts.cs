using CyrodiilRP.Common.Chat;
using ReadyM.Api.Multiplayer;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Common;

[RpcContracts]
public static partial class RpContracts
{
    // Speech, or a command when it starts with a slash.
    [ClientToServer]
    public static partial void SubmitMessage(string text);

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

    // The server picks whom: the nearest player or trader in front.
    [ClientToServer]
    public static partial void OpenInteractionWheel();

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
}
