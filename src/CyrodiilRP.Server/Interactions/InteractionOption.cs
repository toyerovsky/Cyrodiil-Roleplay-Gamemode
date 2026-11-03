using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Server.Interactions;

internal sealed class InteractionOption : WheelOption
{
    // Arm's length.
    public const float DefaultRange = 250f;

    public float Reach { get; init; } = DefaultRange;

    public Func<OnlinePlayer, OnlinePlayer, OptionState> State { get; init; } = (_, _) => OptionState.Open;

    public required Action<OnlinePlayer, OnlinePlayer> Run { get; init; }

    public override float? Range => Reach;

    public override OptionState Check(OnlinePlayer actor, OnlinePlayer target) => State(actor, target);

    public override void Execute(OnlinePlayer actor, OnlinePlayer target) => Run(actor, target);
}
