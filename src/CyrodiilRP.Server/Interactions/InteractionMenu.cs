using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Lore;
using CyrodiilRP.Server.Network;
using CyrodiilRP.Server.Players;
using ReadyM.Api.Idents;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Interactions;

// The wheel over the player in front. The server picks whom from the positions it holds, so the client only asks.
[Service]
internal sealed partial class InteractionMenu(
    RpcHandlers rpc,
    OnlinePlayers online,
    Proximity proximity,
    RadialWheel wheel,
    WheelRules rules,
    ShownNames names)
{
    // A few steps ahead, within a quarter turn either side of where the character faces.
    public const float Reach = 300f;
    public const float ConeDegrees = 90f;

    private readonly WheelOptions _options = new(rules);
    private readonly List<IFrontTarget> _targets = [];

    public void Add(InteractionOption option) => _options.Add(option);

    public void AddTarget(IFrontTarget target) => _targets.Add(target);

    private void Start() => rpc.InteractionWheelAsked += OnAsked;

    private void OnAsked(PlayerId id)
    {
        if (!online.TryGet(id, out var actor) || !actor.IsLoaded)
            return;

        if (actor.Character is null)
        {
            rules.Notify(actor, ChatText.NeedCharacter);
            return;
        }

        // The welcome holds the player's input, so only a client that ignores it asks from there.
        if (!actor.IsPlaying)
            return;

        OnlinePlayer? target = null;
        if (proximity.FindNearestInFront(actor.Id, WheelTargets.Filter(online.All).Select(p => p.Id), Reach, ConeDegrees) is { } targetId)
            online.TryGet(targetId, out target);

        if (FindNearestTarget(actor, target) is { } other)
        {
            other.Use(actor, Reach, ConeDegrees);
            return;
        }

        if (target is null)
        {
            rules.Notify(actor, InteractionsText.NobodyInFront);
            return;
        }

        var items = _options.BuildItems(actor, target);
        if (items.Count == 0)
        {
            rules.Notify(actor, InteractionsText.Nothing);
            return;
        }

        wheel.Open(actor, new WheelRequest(names.Shown(actor, target), items, (picker, itemId) => _options.Pick(picker, target, itemId)));
    }

    // Another thing in front, when it stands nearer than the player in front, or there is none.
    private IFrontTarget? FindNearestTarget(OnlinePlayer actor, OnlinePlayer? player)
    {
        var nearest = player is not null && proximity.TryLocate(player.Id, out var area, out var position)
            ? proximity.MeasureDistance(actor.Id, area, position) ?? float.MaxValue
            : float.MaxValue;

        IFrontTarget? found = null;
        foreach (var target in _targets)
        {
            if (target.MeasureInFront(actor, Reach, ConeDegrees) is { } distance && distance < nearest)
            {
                found = target;
                nearest = distance;
            }
        }

        return found;
    }
}
