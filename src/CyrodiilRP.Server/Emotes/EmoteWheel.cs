using CyrodiilRP.Server.Interactions;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Players;
using ReadyM.SDK.Attributes;
using Roleplay.Emotes.Server;

namespace CyrodiilRP.Server.Emotes;

// Emotes on the G wheel open a wheel of every emote of the Emotes mod, Stop first. The page pages it, eight a page.
[Service]
internal sealed partial class EmoteWheel(SelfMenu self, RadialWheel wheel, WheelRules rules, IEmotes emotes)
{
    private const string EmotesOption = "self.emotes";
    private const int EmotesOrder = 10;

    // The item ids: Stop, or an emote's id after the prefix, so no emote id can be taken for Stop
    private const string StopItem = "stop";
    private const string EmotePrefix = "emote:";

    private void Start() => self.Add(new SelfOption
    {
        Id = EmotesOption,
        Label = EmotesText.Option,
        Icon = WheelIcons.Emote,
        Order = EmotesOrder,
        Run = Open,
    });

    private void Open(OnlinePlayer player)
    {
        var items = new List<WheelItemView> { new(StopItem, rules.Translate(player, EmotesText.Stop), WheelIcons.Stop, Disabled: false, Reason: "") };
        items.AddRange(emotes.All.Select(emote => new WheelItemView(
            EmotePrefix + emote.Id, emote.NameIn(player.Locale), emote.Icon ?? WheelIcons.Emote, Disabled: false, Reason: "")));

        wheel.Open(player, new WheelRequest(rules.Translate(player, EmotesText.Title), items, Pick));
    }

    private void Pick(OnlinePlayer player, string itemId)
    {
        if (itemId == StopItem)
        {
            emotes.Stop(player.Id);
            return;
        }

        if (!itemId.StartsWith(EmotePrefix, StringComparison.Ordinal))
            throw new InvalidOperationException($"The emote wheel offered {itemId}, which is neither Stop nor an emote.");

        if (emotes.Play(player.Id, itemId[EmotePrefix.Length..]) is { } refusal)
            rules.Notify(player, Describe(refusal));
    }

    private static Phrase Describe(EmoteRefusal refusal) => refusal switch
    {
        EmoteRefusal.Unknown => EmotesText.ErrUnknown,
        EmoteRefusal.NotInWorld => EmotesText.ErrNotInWorld,
    };
}
