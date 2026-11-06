using System.Numerics;
using OblivionMP.SDK.Common.Foliage;
using ReadyM.SDK.Archetypes.Core;
using ReadyM.SDK.Attributes;

namespace Roleplay.Jobs.Common;

/// <summary>
/// A tree comes down. The server raises it, as the player whose hit felled it, once the tree has taken the hits it needs.
/// </summary>
/// <remarks>
/// <para>Jobs checks the tree still stands (<c>Stage.Validate</c>), then on the server takes it out for everyone, leaves
/// a stump and shares the wood among everyone who hit it (<c>Stage.Apply</c>); every client near plays it falling
/// (<c>Stage.Apply</c>). Area scope: the players around the feller hear of it.</para>
/// <para>Another mod can refuse a felling in <c>Stage.Validate</c> (a protected grove, a quest tree), react to one in
/// <c>Stage.Observe</c> (experience, a contract), and hear of a refused one in <c>Stage.Rejected</c>.</para>
/// </remarks>
[NetworkedEvent(EventScope.Area, Policy.ServerAuthoritative, Reject = typeof(HarvestRefusal))]
public partial struct TreeFelledEvent
{
    /// <summary>The player whose hit felled it. Everyone who hit it shares the wood.</summary>
    [Subject] public Player Feller { get; set; }

    /// <summary>The tree's mesh, a part of its <see cref="FoliageId"/>: <see cref="JobEvents.Tree"/> puts it together.</summary>
    public string Mesh { get; set; }

    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }

    /// <summary>The kind of tree as the server's config names it, such as "Oak"; empty for a tree of no listed kind.</summary>
    public string Kind { get; set; }

    /// <summary>The tree's own scale, 1 for its model as made.</summary>
    public float Scale { get; set; }

    /// <summary>Which way it falls, in Unreal yaw degrees: away from the feller.</summary>
    public float FallYaw { get; set; }
}

/// <summary>
/// An ore node is spent. The server raises it, as the player whose hit spent it, once the node has taken the hits it
/// needs. Jobs checks the node still stands, then takes it out for everyone and shares what it gives among everyone who
/// hit it, the best of it to whoever struck most; every client near sees it burst. Other mods hook in as on
/// <see cref="TreeFelledEvent"/>.
/// </summary>
[NetworkedEvent(EventScope.Area, Policy.ServerAuthoritative, Reject = typeof(HarvestRefusal))]
public partial struct OreMinedEvent
{
    [Subject] public Player Miner { get; set; }

    /// <summary>The node's id, as Jobs names it.</summary>
    public string Node { get; set; }

    /// <summary>The node's kind, such as "Iron".</summary>
    public string Kind { get; set; }

    public Vector3 Position { get; set; }

    public float Scale { get; set; }
}

/// <summary>
/// A block of stone comes loose in a quarry: the server raises it, as the player, at every few hits they land on rock
/// there. Jobs gives them the quarry's yield. Other mods hook in as on <see cref="TreeFelledEvent"/>.
/// </summary>
[NetworkedEvent(EventScope.Area, Policy.ServerAuthoritative, Reject = typeof(HarvestRefusal))]
public partial struct StoneQuarriedEvent
{
    [Subject] public Player Quarrier { get; set; }

    /// <summary>The quarry's name: its map marker's, or the config's.</summary>
    public string Quarry { get; set; }

    /// <summary>Where the last hit landed.</summary>
    public Vector3 Position { get; set; }
}

/// <summary>Why a tree, node or quarry yield did not come about.</summary>
public enum HarvestRefusal : byte
{
    /// <summary>It is gone already: felled or mined by someone else a moment before.</summary>
    Gone = 0,

    /// <summary>Another mod refused it.</summary>
    Other = 1,
}

public static class JobEvents
{
    /// <summary>The felled tree's id.</summary>
    public static FoliageId Tree(this in TreeFelledEvent felled) => new(felled.Mesh, felled.X, felled.Y, felled.Z);

    public static string Describe(HarvestRefusal refusal) => refusal switch
    {
        HarvestRefusal.Gone => "Someone got to it first.",
        HarvestRefusal.Other => "Something stops it.",
        _ => throw new ArgumentOutOfRangeException(nameof(refusal), refusal, "Not a harvest refusal."),
    };
}

/// <summary>A place marked for a job: a quarry, a lumber camp. What lies within <paramref name="Radius"/> centimeters of
/// <paramref name="Center"/> (the Unreal world) is in it.</summary>
public readonly record struct MarkedArea(string Name, Vector3 Center, float Radius)
{
    public bool Holds(Vector3 point) => Vector3.Distance(point, Center) <= Radius;
}
