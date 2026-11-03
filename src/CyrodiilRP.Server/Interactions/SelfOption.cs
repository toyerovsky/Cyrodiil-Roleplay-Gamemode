using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Server.Interactions;

internal sealed class SelfOption : WheelOption
{
    public Func<OnlinePlayer, OptionState> State { get; init; } = _ => OptionState.Open;

    public required Action<OnlinePlayer> Run { get; init; }

    public override OptionState Check(OnlinePlayer actor, OnlinePlayer target) => State(actor);

    public override void Execute(OnlinePlayer actor, OnlinePlayer target) => Run(actor);
}
