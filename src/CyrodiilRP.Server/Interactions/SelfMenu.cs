using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Network;
using CyrodiilRP.Server.Players;
using ReadyM.Api.Idents;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Interactions;

[Service]
internal sealed partial class SelfMenu(
    RpcHandlers rpc,
    OnlinePlayers online,
    RadialWheel wheel,
    WheelRules rules)
{
    private readonly WheelOptions _options = new(rules);

    public void Add(SelfOption option) => _options.Add(option);

    private void Start() => rpc.SelfWheelAsked += OnAsked;

    private void OnAsked(PlayerId id)
    {
        if (!online.TryGet(id, out var player) || !player.IsLoaded)
            return;

        if (player.Character is null)
        {
            rules.Notify(player, ChatText.NeedCharacter);
            return;
        }

        if (!player.IsPlaying)
            return;

        var items = _options.BuildItems(player, player);
        if (items.Count == 0)
        {
            rules.Notify(player, InteractionsText.Nothing);
            return;
        }

        wheel.Open(player, new WheelRequest(player.Name, items, (actor, itemId) => _options.Pick(actor, actor, itemId)));
    }
}
