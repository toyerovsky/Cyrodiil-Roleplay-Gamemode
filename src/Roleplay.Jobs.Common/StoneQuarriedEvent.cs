using System.Numerics;
using OblivionMP.SDK.Common.Archetypes;
using ReadyM.SDK.Attributes;

namespace Roleplay.Jobs.Common;

/// <summary>
/// A block of stone comes loose in a quarry: the server raises it, as the player, at every few hits they land on rock
/// there. Jobs gives them the quarry's yield. Other mods hook in as on <see cref="TreeFelledEvent"/>.
/// </summary>
[NetworkedEvent(EventScope.Area, Policy.ServerAuthoritative, Reject = typeof(HarvestRefusal))]
public partial struct StoneQuarriedEvent
{
    /// <summary>The character who quarried it (<see cref="JobEvents.PlayerOf"/> gives its player).</summary>
    [Subject] public Character Quarrier { get; set; }

    /// <summary>The quarry's name: its map marker's, or the config's.</summary>
    public string Quarry { get; set; }

    /// <summary>Where the last hit landed.</summary>
    public Vector3 Position { get; set; }
}
