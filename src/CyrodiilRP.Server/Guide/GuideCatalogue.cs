using System.Globalization;
using CyrodiilRP.Data;
using CyrodiilRP.Server.Bank;
using CyrodiilRP.Server.Dice;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Players;
using CyrodiilRP.Server.Traders;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Guide;

// Every guide with the numbers its texts and plates show, read from the rules and the config that run the window.
[Service]
internal sealed partial class GuideCatalogue(RpConfig config)
{
    // The mode's keys (the client's binds and the menu's Keys tab) as the keyboard reads them.
    public const string SelfKey = "G";
    public const string InteractionKey = "H";
    public const string ChatKey = "K";
    public const string YesKey = "Y";
    public const string NoKey = "N";
    public const string MenuKey = "F10";
    public const string DiceKey = "Home";
    public const string LoreKey = "End";
    public const string LoreLookKey = "Shift+End";
    public const string CloseKey = "Esc";
    public const string RollCommand = "/roll";
    public const string DiceCommand = "/dice";

    private const int SecondsPerMinute = 60;
    private const int SecondsPerHour = 3600;
    private const int SecondsPerDay = 86400;

    // From five figures on a number is grouped by threes, with a space that reads the same in English and Polish and never
    // breaks the number over two lines.
    private const int GroupedFrom = 10_000;
    private const string GroupSeparator = "\u00A0";
    private static readonly NumberFormatInfo Grouping = new() { NumberGroupSeparator = GroupSeparator, NumberGroupSizes = [3] };

    public static IReadOnlyList<GuideTopic> All { get; } = Enum.GetValues<GuideTopic>();

    public GuideCard Build(GuideTopic topic) => topic switch
    {
        GuideTopic.Welcome => new(topic, GuidesText.WelcomeTitle, GuidesText.WelcomePurpose,
            [
                Step(GuidesText.WelcomeSelf, SelfKey),
                Step(GuidesText.WelcomeInteract, InteractionKey),
                Step(GuidesText.WelcomeChat, ChatKey),
                Step(GuidesText.WelcomeAnswer, YesKey, NoKey),
                Step(GuidesText.WelcomeMenu, MenuKey),
                Step(GuidesText.WelcomeDice, DiceKey),
                Step(GuidesText.WelcomeLore, LoreKey),
                Step(GuidesText.WelcomeTalk),
            ],
            []),
        GuideTopic.Trader => new(topic, GuidesText.TraderTitle, GuidesText.TraderPurpose,
            [Step(GuidesText.TraderStep1), Step(GuidesText.TraderStep2), Step(GuidesText.TraderStep3), Step(GuidesText.TraderStep4)],
            [
                new(GuidesText.TraderFactPieces, GuidesText.ValueCount(TraderRules.MostPieces)),
                new(GuidesText.TraderFactReach, GuidesText.ValueMetres(TraderRules.WalkAwayMetres)),
            ]),
        GuideTopic.Bank => new(topic, GuidesText.BankTitle, GuidesText.BankPurpose,
            [Step(GuidesText.BankStep1), Step(GuidesText.BankStep2), Step(GuidesText.BankStep3)],
            [
                new(GuidesText.BankFactMost, Gold(BankRules.MostAtOnce)),
                new(GuidesText.BankFactFee, GuidesText.ValueFree),
            ]),
        GuideTopic.Stable => new(topic, GuidesText.StableTitle, GuidesText.StablePurpose,
            [Step(GuidesText.StableStep1), Step(GuidesText.StableStep2), Step(GuidesText.StableStep3, SelfKey), Step(GuidesText.StableStep4)],
            [
                new(GuidesText.StableFactCheapest, Gold(config.Stable.Paint)),
                new(GuidesText.StableFactDearest, Gold(config.Stable.Black)),
            ]),
        GuideTopic.Mentor => new(topic, GuidesText.MentorTitle, GuidesText.MentorPurpose,
            [
                Step(GuidesText.MentorStep1),
                Step(GuidesText.MentorStep2(config.Specializations.TrainedSkillCap, config.Specializations.LimitedSkillCap)),
                Step(GuidesText.MentorStep3(config.Specializations.UnchosenSkillCap)),
                Step(GuidesText.MentorStep4),
            ],
            [
                new(GuidesText.MentorFactFirst, Gold(config.Specializations.FirstPrice)),
                new(GuidesText.MentorFactChange, Gold(config.Specializations.ChangePrice)),
                new(GuidesText.MentorFactWait, Duration(config.Specializations.ChangeCooldown)),
            ]),
        GuideTopic.Foreman => new(topic, GuidesText.ForemanTitle, GuidesText.ForemanPurpose,
            [Step(GuidesText.ForemanStep1), Step(GuidesText.ForemanStep2), Step(GuidesText.ForemanStep3), Step(GuidesText.ForemanStep4)],
            []),
        GuideTopic.NoticeBoard => new(topic, GuidesText.NoticeBoardTitle, GuidesText.NoticeBoardPurpose,
            [
                Step(GuidesText.NoticeBoardStep1, InteractionKey),
                Step(GuidesText.NoticeBoardStep2(config.NoticeBoards.MaxLength)),
                Step(GuidesText.NoticeBoardStep3),
                Step(GuidesText.NoticeBoardStep4),
            ],
            [
                new(GuidesText.NoticeBoardFactYours, GuidesText.ValueCount(config.NoticeBoards.PerCharacter)),
                new(GuidesText.NoticeBoardFactWait, GuidesText.ValueMinutes(config.NoticeBoards.CooldownMinutes)),
                new(GuidesText.NoticeBoardFactStays, GuidesText.ValueDays(config.NoticeBoards.ExpiryDays)),
                new(GuidesText.NoticeBoardFactShown, GuidesText.ValueCount(config.NoticeBoards.Shown)),
            ]),
        GuideTopic.Gambling => new(topic, GuidesText.GamblingTitle, GuidesText.GamblingPurpose,
            [
                Step(GuidesText.GamblingStep1, SelfKey, InteractionKey),
                Step(GuidesText.GamblingStep2, YesKey, NoKey),
                Step(GuidesText.GamblingStep3),
                Step(GuidesText.GamblingStep4),
                Step(GuidesText.GamblingStep5, CloseKey),
            ],
            [
                new(GuidesText.GamblingFactStake, GuidesText.ValueRange(Number(config.Gambling.MinStake), Number(config.Gambling.MaxStake))),
                new(GuidesText.GamblingFactRounds, GuidesText.ValueCount(config.Gambling.PokerRoundsToWin)),
                new(GuidesText.GamblingFactAnswer, GuidesText.ValueSeconds(config.Gambling.InviteSeconds)),
                new(GuidesText.GamblingFactCut, GuidesText.ValuePercent(config.Gambling.CutPercent)),
            ]),
        GuideTopic.Dice => new(topic, GuidesText.DiceTitle, GuidesText.DicePurpose,
            [
                Step(GuidesText.DiceStep1),
                Step(GuidesText.DiceStep2(RollNotation.MaxDice)),
                Step(GuidesText.DiceStep3, RollCommand),
                Step(GuidesText.DiceStep4, MenuKey, DiceCommand),
            ],
            [
                new(GuidesText.DiceFactMost, GuidesText.ValueCount(RollNotation.MaxDice)),
                new(GuidesText.DiceFactSets, GuidesText.ValueCount(DiceSets.All.Count)),
            ]),
        GuideTopic.Lore => new(topic, GuidesText.LoreTitle, GuidesText.LorePurpose,
            [Step(GuidesText.LoreStep1), Step(GuidesText.LoreStep2), Step(GuidesText.LoreStep3, InteractionKey), Step(GuidesText.LoreStep4, LoreLookKey)],
            [new(GuidesText.LoreFactAnswer, GuidesText.ValueSeconds(AskPacing.TimeoutSeconds))]),
        GuideTopic.Exchange => new(topic, GuidesText.ExchangeTitle, GuidesText.ExchangePurpose,
            [Step(GuidesText.ExchangeStep1, InteractionKey), Step(GuidesText.ExchangeStep2), Step(GuidesText.ExchangeStep3), Step(GuidesText.ExchangeStep4)],
            [
                new(GuidesText.ExchangeFactMost, Gold(config.Economy.MaxPayment)),
                new(GuidesText.ExchangeFactAnswer, GuidesText.ValueSeconds(AskPacing.TimeoutSeconds)),
            ]),
    };

    // The largest unit the time is a whole number of: three days, not 72 h.
    public static Phrase Duration(TimeSpan time)
    {
        var seconds = (long)time.TotalSeconds;
        if (seconds >= SecondsPerDay && seconds % SecondsPerDay == 0)
            return GuidesText.ValueDays(seconds / SecondsPerDay);
        if (seconds >= SecondsPerHour && seconds % SecondsPerHour == 0)
            return GuidesText.ValueHours(seconds / SecondsPerHour);
        if (seconds >= SecondsPerMinute && seconds % SecondsPerMinute == 0)
            return GuidesText.ValueMinutes(seconds / SecondsPerMinute);

        return GuidesText.ValueSeconds(seconds);
    }

    public static string Number(int value) => value < GroupedFrom ? value.ToString(CultureInfo.InvariantCulture) : value.ToString("#,0", Grouping);

    private static Phrase Gold(int price) => price == 0 ? GuidesText.ValueFree : GuidesText.ValueGold(Number(price));

    private static GuideStep Step(Phrase text, params string[] keys) => new(text, keys);
}
