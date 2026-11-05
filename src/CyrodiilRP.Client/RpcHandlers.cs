using CyrodiilRP.Common;
using CyrodiilRP.Common.Chat;
using OblivionMp.Sdk;
using OblivionMpCSharpMod;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client;

// What the services act on is raised as events, so they send through this class without needing each other.
[RpcHandlersFor(typeof(RpContracts))]
internal sealed partial class RpcHandlers(IUserInterface ui)
{
    // Long enough to read a line, short enough not to cover the next one.
    private const float NoticeSeconds = 5f;

    public event Action<ChatLine>? MessageDelivered;

    public event Action<string>? LocaleChanged;

    public event Action? NamingStarted;

    public event Action<string>? NameAnswered;

    public event Action? AppearanceStarted;

    public event Action<string>? WheelShown;

    public event Action<string>? ChatPrefilled;

    public event Action<string>? ExchangeChanged;

    public event Action<string>? ExchangeRefused;

    public event Action<string>? ExchangeDone;

    public event Action? ExchangeClosed;

    public event Action<string>? PartyChanged;

    public event Action<string, int>? AsksChanged;

    public event Action<int, string, int>? OverheadShown;

    public event Action<string>? TraderChanged;

    public event Action<string, bool>? TraderAnswered;

    public event Action? TraderClosed;

    public event Action<string>? TraderLabelled;

    public event Action<string>? MenuChanged;

    public event Action<string>? AdminChanged;

    public event Action<string, string>? AdminPartArrived;

    public event Action<string, bool>? AdminAnswered;

    public event Action? AdminClosed;

    public event Action<string>? FrozenByStaff;

    public event Action? ThawedByStaff;

    public event Action? RevivedByStaff;

    public event Action<string>? WelcomeShown;

    public event Action? WelcomeClosed;

    public event Action<string>? DiceChanged;

    public event Action? DiceBoardClosed;

    public event Action<int, int[]>? DiceThrown;

    public event Action<int, string>? DiceRefused;

    public event Action<int, int, int, int>? DiceThrownOnBoard;

    public event Action<string, int, int, int, int, int[]>? DicePresented;

    partial void OnDeliverMessage(ChatLine line) => MessageDelivered?.Invoke(line);

    partial void OnNotify(string text) => ui.ShowMessage(text, MessagePosition.TopLeft, NoticeSeconds);

    partial void OnLocale(string locale) => LocaleChanged?.Invoke(locale);

    partial void OnStartNaming() => NamingStarted?.Invoke();

    partial void OnNameAnswer(string problem) => NameAnswered?.Invoke(problem);

    partial void OnStartAppearance() => AppearanceStarted?.Invoke();

    partial void OnShowWheel(string json) => WheelShown?.Invoke(json);

    partial void OnPrefillChat(string text) => ChatPrefilled?.Invoke(text);

    partial void OnExchangeState(string json) => ExchangeChanged?.Invoke(json);

    partial void OnExchangeRefused(string text) => ExchangeRefused?.Invoke(text);

    partial void OnExchangeDone(string text) => ExchangeDone?.Invoke(text);

    partial void OnExchangeClosed() => ExchangeClosed?.Invoke();

    partial void OnPartyState(string json) => PartyChanged?.Invoke(json);

    partial void OnAsksState(string json, int answerable) => AsksChanged?.Invoke(json, answerable);

    partial void OnOverhead(int speaker, string text, int showMs) => OverheadShown?.Invoke(speaker, text, showMs);

    partial void OnTraderState(string json) => TraderChanged?.Invoke(json);

    partial void OnTraderAnswer(string text, bool bought) => TraderAnswered?.Invoke(text, bought);

    partial void OnTraderClosed() => TraderClosed?.Invoke();

    partial void OnTraderLabel(string text) => TraderLabelled?.Invoke(text);

    partial void OnMenuState(string json) => MenuChanged?.Invoke(json);

    partial void OnAdminState(string json) => AdminChanged?.Invoke(json);

    partial void OnAdminPart(string kind, string json) => AdminPartArrived?.Invoke(kind, json);

    partial void OnAdminAnswer(string text, bool done) => AdminAnswered?.Invoke(text, done);

    partial void OnAdminClosed() => AdminClosed?.Invoke();

    partial void OnFrozen(string notice) => FrozenByStaff?.Invoke(notice);

    partial void OnThawed() => ThawedByStaff?.Invoke();

    partial void OnRevive() => RevivedByStaff?.Invoke();

    partial void OnWelcome(string json) => WelcomeShown?.Invoke(json);

    partial void OnWelcomeClosed() => WelcomeClosed?.Invoke();

    partial void OnDiceState(string json) => DiceChanged?.Invoke(json);

    partial void OnCloseDiceBoard() => DiceBoardClosed?.Invoke();

    partial void OnDiceThrown(int requestId, int[] values) => DiceThrown?.Invoke(requestId, values);

    partial void OnDiceRefused(int requestId, string text) => DiceRefused?.Invoke(requestId, text);

    partial void OnThrowOnBoard(int d6, int d10, int d20, int d100) => DiceThrownOnBoard?.Invoke(d6, d10, d20, d100);

    partial void OnPresentDice(string reason, int d6, int d10, int d20, int d100, int[] values) =>
        DicePresented?.Invoke(reason, d6, d10, d20, d100, values);
}
