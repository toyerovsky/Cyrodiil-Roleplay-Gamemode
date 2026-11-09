using System.Globalization;
using CyrodiilRP.Common.Chat;
using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Interactions;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Players;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Events;
using Roleplay.HungerAndThirst.Common;
using Roleplay.HungerAndThirst.Server;

namespace CyrodiilRP.Server.Sustenance;

// Feeding the player in front from one's own pack, through the Hunger and Thirst mod: Feed on the H wheel, then a wheel of the foods carried.
[Service]
internal sealed partial class Feeding(
    InteractionMenu interactions,
    RadialWheel wheel,
    WheelRules rules,
    OnlinePlayers online,
    RpChat chat,
    RpConfig config,
    ISustenance sustenance)
{
    private const string FeedOption = "player.feed";
    private const int FeedOrder = 35;

    private void Start() => interactions.Add(new InteractionOption
    {
        Id = FeedOption,
        Label = FeedingText.Option,
        Icon = WheelIcons.Bread,
        Order = FeedOrder,
        State = (actor, target) => OptionState.OpenUnless(CheckFeed(actor, target)),
        Run = OpenPack,
    });

    // The first food stands for all of them: what refuses one (too far, chewing, dead) refuses every one.
    private Phrase? CheckFeed(OnlinePlayer actor, OnlinePlayer target)
    {
        if (sustenance.CarriedBy(actor.Id) is not [var first, ..])
            return FeedingText.ErrNothing;

        return sustenance.Check(actor.Id, target.Id, first.Food.FormId.Value) is { } refusal ? Describe(refusal, target) : null;
    }

    // What the one in front needs most comes first: drink for the thirstier, food for the hungrier.
    private void OpenPack(OnlinePlayer actor, OnlinePlayer target)
    {
        var thirstier = sustenance.TryGetMeters(target.Id, out var meters) && meters.Hydration < meters.Satiety;
        var items = sustenance.CarriedBy(actor.Id)
            .OrderByDescending(carried => thirstier ? carried.Food.Hydration : carried.Food.Satiety)
            .Select(carried => ToItem(actor, target, carried))
            .ToList();
        if (items.Count == 0)
        {
            rules.Notify(actor, FeedingText.ErrNothing);
            return;
        }

        wheel.Open(actor, new WheelRequest(rules.Translate(actor, FeedingText.Title(target.Name)), items, (picker, id) => Give(picker, target, id)));
    }

    private WheelItemView ToItem(OnlinePlayer actor, OnlinePlayer target, CarriedFood carried)
    {
        var formId = carried.Food.FormId.Value;
        var refusal = sustenance.Check(actor.Id, target.Id, formId) is { } reason ? rules.Translate(actor, Describe(reason, target)) : "";
        return new WheelItemView(
            formId.ToString(CultureInfo.InvariantCulture),
            rules.Translate(actor, FeedingText.Item(carried.Food.Name, carried.Count)),
            carried.Food.Hydration > carried.Food.Satiety ? WheelIcons.Flask : WheelIcons.Bread,
            refusal.Length > 0,
            refusal);
    }

    private void Give(OnlinePlayer actor, OnlinePlayer target, string itemId)
    {
        if (!target.IsLive)
        {
            rules.Notify(actor, InteractionsText.Gone);
            return;
        }

        if (sustenance.Feed(actor.Id, target.Id, uint.Parse(itemId, CultureInfo.InvariantCulture)) is { } refusal)
            rules.Notify(actor, Describe(refusal, target));
    }

    // Every feeding of one player by another, from the wheel or from a mod's own UI on the client.
    [EventHandler(Stage.Observe)]
    public void OnMeal(in MealEvent meal, EventContext context)
    {
        if (meal.Feeder.PlayerId == meal.Eater.PlayerId
            || !online.TryGet(meal.Feeder.PlayerId, out var feeder)
            || !online.TryGet(meal.Eater.PlayerId, out var eater))
            return;

        var formId = meal.FormId;
        var food = sustenance.Foods.First(item => item.FormId.Value == formId);
        chat.SendAround(feeder, config.Chat.Emote, ChatLineKind.Action, FeedingText.Fed(feeder.Name, eater.Name, food.Name));
    }

    private static Phrase Describe(MealRefusal refusal, OnlinePlayer target) => refusal switch
    {
        MealRefusal.NotInWorld => FeedingText.ErrNotInWorld(target.Name),
        MealRefusal.FeederDead => FeedingText.ErrFeederDead,
        MealRefusal.EaterDead => FeedingText.ErrEaterDead(target.Name),
        MealRefusal.TooSoon => FeedingText.ErrTooSoon(target.Name),
        MealRefusal.NotFood => FeedingText.ErrNotFood,
        MealRefusal.NotCarried => FeedingText.ErrNotCarried,
        MealRefusal.NoWater => FeedingText.ErrWater,
        MealRefusal.WaterForOthers => FeedingText.ErrWater,
        MealRefusal.TooFar => FeedingText.ErrTooFar(target.Name),
        MealRefusal.Other => FeedingText.ErrOther(target.Name),
    };
}
