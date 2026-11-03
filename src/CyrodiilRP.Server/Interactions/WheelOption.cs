using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Server.Interactions;

// What every wedge has. The own wheel's target is the player.
internal abstract class WheelOption
{
    public required string Id { get; init; }

    public required Phrase Label { get; init; }

    public required string Icon { get; init; }

    // Lower comes first: the players' own options before the staff's.
    public int Order { get; init; }

    // Null: everyone sees it. Otherwise only a role with this permission.
    public string? Permission { get; init; }

    // Said in character or out, so a mute hides it.
    public bool Speaks { get; init; }

    // How near the target must stand, in centimetres. Null for an option on oneself.
    public virtual float? Range => null;

    public abstract OptionState Check(OnlinePlayer actor, OnlinePlayer target);

    public abstract void Execute(OnlinePlayer actor, OnlinePlayer target);
}
