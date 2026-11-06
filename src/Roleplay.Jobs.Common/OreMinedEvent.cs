using System.Numerics;
using OblivionMP.SDK.Common.Archetypes;
using ReadyM.SDK.Attributes;

namespace Roleplay.Jobs.Common;

/// <summary>
/// An ore node is spent. The server raises it, as the player whose hit spent it, once the node has taken the hits it
/// needs. Jobs checks the node still stands, then takes it out for everyone and shares what it gives among everyone who
/// hit it, the best of it to whoever struck most; every client near sees it burst. Other mods hook in as on
/// <see cref="TreeFelledEvent"/>.
/// </summary>
[NetworkedEvent(EventScope.Area, Policy.ServerAuthoritative, Reject = typeof(HarvestRefusal))]
public partial struct OreMinedEvent
{
    /// <summary>The character whose hit spent it (<see cref="JobEvents.PlayerOf"/> gives its player).</summary>
    [Subject] public Character Miner { get; set; }

    /// <summary>The node's id, as Jobs names it.</summary>
    public string Node { get; set; }

    /// <summary>The node's kind, such as "Iron".</summary>
    public string Kind { get; set; }

    public Vector3 Position { get; set; }

    public float Scale { get; set; }
}
