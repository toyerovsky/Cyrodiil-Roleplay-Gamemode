using ReadyM.SDK.Archetypes.Core;
using ReadyM.SDK.Attributes;

namespace Roleplay.HungerAndThirst.Common;

/// <summary>
/// Someone eats or drinks one item from the feeder's pack: their own meal when <see cref="Feeder"/> and
/// <see cref="Eater"/> are the same player, or one player feeding another nearby. <see cref="FormId"/> 0 is a sip from
/// the water the feeder is swimming in, for themselves only.
/// </summary>
/// <remarks>
/// <para>Raised by the feeder's client (the satchel, or any mod through <c>ILocalSustenance</c>), or by a server mod as
/// the feeder through <c>ISustenance.Feed</c>. The hunger server checks it (<see cref="MealRefusal"/>), takes the item
/// from the feeder and fills the eater. Area scope, so the eater and everyone nearby hear about it in
/// <c>Stage.Observe</c>.</para>
/// <para>Other mods can add a <c>Stage.Validate</c> to refuse a meal or a <c>Stage.Observe</c> to react to one; a
/// refusal of theirs reaches the raising client's <c>Stage.Rejected</c> like the mod's own.</para>
/// </remarks>
[NetworkedEvent(EventScope.Area, Policy.SubjectOwner, Reject = typeof(MealRefusal))]
public partial struct MealEvent
{
    /// <summary>Whose pack the item comes from, and who raised the event.</summary>
    [Subject] public Player Feeder { get; set; }

    /// <summary>Who it fills. The same as <see cref="Feeder"/> for a meal of one's own.</summary>
    public Player Eater { get; set; }

    /// <summary>The food or drink's form id, or 0 for water.</summary>
    public uint FormId { get; set; }
}
