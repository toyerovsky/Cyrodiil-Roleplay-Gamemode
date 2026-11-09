using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Server.Interactions;

// A missing permission, a mute over an option that speaks, or a target out of reach, hides an option. Otherwise its
// State hides it, shows it closed with the reason, or open. A pick is checked again before it runs: the wheel stayed
// open a while.
internal sealed class WheelOptions(IWheelRules rules)
{
    // The places on a page of a wheel. A wheel of more turns its pages on the page (ui/radial/paging.js).
    public const int PageSize = 8;

    // Two characters' positions are their middles, a body's width apart when they touch.
    private const float BodySlack = 75f;

    private readonly List<WheelOption> _options = [];

    // An id taken twice is a bug, so it throws.
    public void Add(WheelOption option)
    {
        if (_options.Any(o => o.Id == option.Id))
            throw new InvalidOperationException($"The wheel option {option.Id} is added twice.");

        _options.Add(option);
        _options.Sort((a, b) => a.Order.CompareTo(b.Order));
    }

    public List<WheelItemView> BuildItems(OnlinePlayer actor, OnlinePlayer target)
    {
        var items = new List<WheelItemView>();
        foreach (var option in _options.Where(o => IsReachable(actor, target, o)))
        {
            var state = option.Check(actor, target);
            if (!state.IsShown)
                continue;

            var reason = state.Refusal is { } refusal ? rules.Translate(actor, refusal) : "";
            items.Add(new WheelItemView(option.Id, rules.Translate(actor, option.Label), option.Icon, !state.IsOpen, reason));
        }

        return items;
    }

    public void Pick(OnlinePlayer actor, OnlinePlayer target, string id)
    {
        if (!actor.IsLive || _options.FirstOrDefault(o => o.Id == id) is not { } option)
            return;

        if (!target.IsLive || !target.IsLoaded || !IsReachable(actor, target, option))
        {
            rules.Notify(actor, InteractionsText.Gone);
            return;
        }

        var state = option.Check(actor, target);
        if (!state.IsOpen)
        {
            rules.Notify(actor, state.Refusal ?? InteractionsText.Gone);
            return;
        }

        option.Execute(actor, target);
    }

    private bool IsReachable(OnlinePlayer actor, OnlinePlayer target, WheelOption option) =>
        (option.Permission is not { } permission || rules.Allows(actor, permission))
        && (!option.Speaks || rules.MaySpeak(actor))
        && (option.Range is not { } range || rules.AreNear(actor, target, range + BodySlack));
}
