namespace Roleplay.Jobs.Common;

/// <summary>How a hit on a tree, an ore node or quarry rock was made, as the client reports it: the SDK's AttackKind,
/// which the server cannot see. Only these two count.</summary>
public enum JobAttack : byte
{
    /// <summary>A normal attack: it always lands, and does a little.</summary>
    Normal = 0,

    /// <summary>A power attack: it does several times as much, more with Strength, but often makes no impact.</summary>
    Power = 1,
}
