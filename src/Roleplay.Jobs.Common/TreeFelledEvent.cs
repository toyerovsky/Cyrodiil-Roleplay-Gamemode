using OblivionMP.SDK.Common.Archetypes;
using OblivionMP.SDK.Common.Foliage;
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
    /// <summary>The character whose hit felled it (<see cref="JobEvents.PlayerOf"/> gives its player). Everyone who hit
    /// it shares the wood. A character, not a player, because a character is in an area: the players around it hear of
    /// the felling.</summary>
    [Subject] public Character Feller { get; set; }

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
