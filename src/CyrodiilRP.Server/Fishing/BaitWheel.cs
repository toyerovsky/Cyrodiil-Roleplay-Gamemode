using CyrodiilRP.Server.Interactions;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Players;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Fishing;

// Bait on the G wheel while the pack holds any: a wheel of the baits carried, with how many, and a bare hook. The choice
// stays the character's.
[Service]
internal sealed partial class BaitWheel(SelfMenu self, RadialWheel wheel, WheelRules rules, FishingStock stock, FishingSkills skills)
{
    private const string BaitOption = "self.fish_bait";
    private const int BaitOrder = 36;

    private void Start() => self.Add(new SelfOption
    {
        Id = BaitOption,
        Label = FishingText.OptionBait,
        Icon = WheelIcons.Bait,
        Order = BaitOrder,
        State = player => OptionState.OpenIf(skills.Find(player) is not null && stock.ListBaits(player).Count > 0),
        Run = Open,
    });

    private void Open(OnlinePlayer player)
    {
        if (skills.Find(player) is not { } progress)
        {
            rules.Notify(player, FishingText.NotReady);
            return;
        }

        var carried = stock.ListBaits(player);
        if (carried.Count == 0)
        {
            rules.Notify(player, FishingText.BaitNoneCarried);
            return;
        }

        var items = carried
            .Select(bait => ToItem(player, bait.Bait.Kind, FishingText.BaitItem(bait.Bait.Name, bait.Count), WheelIcons.Bait, progress.Bait))
            .Append(ToItem(player, BaitKind.None, FishingText.BaitNone, WheelIcons.Stop, progress.Bait))
            .ToList();
        wheel.Open(player, new WheelRequest(rules.Translate(player, FishingText.BaitTitle), items, Choose));
    }

    private WheelItemView ToItem(OnlinePlayer player, BaitKind kind, Phrase label, string icon, BaitKind chosen)
    {
        var on = kind == chosen;
        return new WheelItemView(kind.ToString(), rules.Translate(player, label), icon, on, on ? rules.Translate(player, FishingText.BaitOn) : "");
    }

    private void Choose(OnlinePlayer player, string itemId)
    {
        if (!Enum.TryParse<BaitKind>(itemId, out var bait) || !Enum.IsDefined(bait))
            throw new InvalidOperationException($"The bait wheel offered {itemId}, which is no bait.");

        if (skills.Find(player) is null)
        {
            rules.Notify(player, FishingText.NotReady);
            return;
        }

        skills.ChooseBait(player, bait);
        rules.Notify(player, bait == BaitKind.None ? FishingText.BaitBare : FishingText.BaitChosen(BaitCatalog.Find(bait).Name));
    }
}
