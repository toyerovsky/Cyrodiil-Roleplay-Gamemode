using System.Numerics;
using CyrodiilRP.Common;
using CyrodiilRP.Common.Gambling;
using CyrodiilRP.Client.Traders;
using CyrodiilRP.Common.Chat;
using OblivionMP.SDK;
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

    public event Action<string>? ForemanChanged;

    public event Action<string, bool>? ForemanAnswered;

    public event Action? ForemanClosed;

    public event Action<string>? TraderLabelled;

    public event Action<string>? BankerLabelled;

    public event Action<string>? BankChanged;

    public event Action<string, bool>? BankAnswered;

    public event Action? BankClosed;

    public event Action<string>? AuctioneerLabelled;

    public event Action<string>? AuctionOpened;

    public event Action<string>? AuctionChanged;

    public event Action<string, bool>? AuctionAnswered;

    public event Action? AuctionClosed;

    public event Action<string>? StablemasterLabelled;

    public event Action<string>? StableChanged;

    public event Action<string, bool>? StableAnswered;

    public event Action? StableClosed;

    public event Action<MentorLabelSet>? MentorsLabelled;

    public event Action<string>? MentorChanged;

    public event Action<string, bool>? MentorAnswered;

    public event Action? MentorClosed;

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

    public event Action<int, string>? LoreAnswered;

    public event Action? LoreCardClosed;

    public event Action<string>? KnownNamesChanged;

    public event Action<string, bool, string>? FactionAnswered;

    public event Action<string, float[]>? NoticeBoardsArrived;

    public event Action<string>? BoardChanged;

    public event Action<string, bool>? BoardAnswered;

    public event Action? BoardClosed;

    // A short notice in the corner, as the server's own.
    public void ShowNotice(string text) => ui.ShowMessage(text, MessagePosition.TopLeft, NoticeSeconds);

    partial void OnDeliverMessage(ChatLine line) => MessageDelivered?.Invoke(line);

    partial void OnNotify(string text) => ShowNotice(text);

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

    partial void OnForemanState(string json) => ForemanChanged?.Invoke(json);

    partial void OnForemanAnswer(string text, bool done) => ForemanAnswered?.Invoke(text, done);

    partial void OnForemanClosed() => ForemanClosed?.Invoke();

    partial void OnTraderLabel(string text) => TraderLabelled?.Invoke(text);

    partial void OnBankerLabel(string text) => BankerLabelled?.Invoke(text);

    partial void OnBankState(string json) => BankChanged?.Invoke(json);

    partial void OnBankAnswer(string text, bool done) => BankAnswered?.Invoke(text, done);

    partial void OnBankClosed() => BankClosed?.Invoke();

    partial void OnAuctioneerLabel(string text) => AuctioneerLabelled?.Invoke(text);

    partial void OnAuctionOpened(string json) => AuctionOpened?.Invoke(json);

    partial void OnAuctionState(string json) => AuctionChanged?.Invoke(json);

    partial void OnAuctionAnswer(string text, bool done) => AuctionAnswered?.Invoke(text, done);

    partial void OnAuctionClosed() => AuctionClosed?.Invoke();

    partial void OnStablemasterLabel(string text) => StablemasterLabelled?.Invoke(text);

    partial void OnStableState(string json) => StableChanged?.Invoke(json);

    partial void OnStableAnswer(string text, bool bought) => StableAnswered?.Invoke(text, bought);

    partial void OnStableClosed() => StableClosed?.Invoke();

    partial void OnMentorLabels(string warrior, string mage, string support, string registrar) =>
        MentorsLabelled?.Invoke(new MentorLabelSet(warrior, mage, support, registrar));

    partial void OnMentorState(string json) => MentorChanged?.Invoke(json);

    partial void OnMentorAnswer(string text, bool chosen) => MentorAnswered?.Invoke(text, chosen);

    partial void OnMentorClosed() => MentorClosed?.Invoke();

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

    partial void OnLoreAnswer(int requestId, string json) => LoreAnswered?.Invoke(requestId, json);

    partial void OnCloseLoreCard() => LoreCardClosed?.Invoke();

    partial void OnKnownNames(string json) => KnownNamesChanged?.Invoke(json);

    partial void OnFactionAnswer(string text, bool done, string menu) => FactionAnswered?.Invoke(text, done, menu);

    partial void OnNoticeBoardsHere(string asset, float[] spots) => NoticeBoardsArrived?.Invoke(asset, spots);

    partial void OnBoardState(string json) => BoardChanged?.Invoke(json);

    partial void OnBoardAnswer(string text, bool done) => BoardAnswered?.Invoke(text, done);

    partial void OnBoardClosed() => BoardClosed?.Invoke();

    public event Action? GamblingInvitationShown;
    public event Action<string>? GamblingChanged;
    public event Action<string>? GamblingRefused;
    partial void OnGamblingState(int kind, string json)
    {
        Action receive = (GamblingMessageKind)kind switch
        {
            GamblingMessageKind.State => () => GamblingChanged?.Invoke(json),
            GamblingMessageKind.Refused => () => GamblingRefused?.Invoke(json),
            GamblingMessageKind.Invitation => () => GamblingInvitationShown?.Invoke(),
        };
        receive();
    }

    public event Action<string>? GuideShown;

    public event Action<int>? AwayWarned;

    public event Action<int>? AwayMarked;

    public event Action? AwayEnded;

    partial void OnGuide(string json) => GuideShown?.Invoke(json);

    partial void OnAwayWarning(int secondsLeft) => AwayWarned?.Invoke(secondsLeft);

    partial void OnAwayMarked(int secondsAway) => AwayMarked?.Invoke(secondsAway);

    partial void OnAwayEnded() => AwayEnded?.Invoke();

    public event Action? FishingLookAsked;

    public event Action<float, float>? FishingAimed;

    public event Action<string>? FishingPageChanged;

    public event Action? FishingBitten;

    public event Action? FishingEnded;

    public event Action<int, bool>? AnglerShown;

    public event Action<int, Vector3>? FishingFloatMoved;

    public event Action<int>? FishingFloatGone;

    public event Action<string, Vector3>? FishingSplashed;

    partial void OnFishingLook() => FishingLookAsked?.Invoke();

    partial void OnFishingAim(float yaw, float waterZ) => FishingAimed?.Invoke(yaw, waterZ);

    partial void OnFishingPage(string json) => FishingPageChanged?.Invoke(json);

    partial void OnFishingBite() => FishingBitten?.Invoke();

    partial void OnFishingEnded() => FishingEnded?.Invoke();

    partial void OnAnglerShown(int angler, bool fishing) => AnglerShown?.Invoke(angler, fishing);

    partial void OnFishingFloat(int angler, float x, float y, float z) => FishingFloatMoved?.Invoke(angler, new Vector3(x, y, z));

    partial void OnFishingFloatGone(int angler) => FishingFloatGone?.Invoke(angler);

    partial void OnFishingSplash(string kind, float x, float y, float z) => FishingSplashed?.Invoke(kind, new Vector3(x, y, z));
}
