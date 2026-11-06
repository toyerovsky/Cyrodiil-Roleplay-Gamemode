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

/// <summary>Why the server refused a <see cref="MealEvent"/>.</summary>
public enum MealRefusal
{
    /// <summary>The feeder or the eater has no character in the world yet.</summary>
    NotInWorld,
    /// <summary>The feeder is dead.</summary>
    FeederDead,
    /// <summary>The eater is dead.</summary>
    EaterDead,
    /// <summary>The eater ate a moment ago (the server's consumeCooldownSeconds).</summary>
    TooSoon,
    /// <summary>The item is not in the server's food table.</summary>
    NotFood,
    /// <summary>The feeder carries none of it.</summary>
    NotCarried,
    /// <summary>Water was asked for, but the feeder isn't swimming.</summary>
    NoWater,
    /// <summary>Water can only be drunk by the one swimming in it.</summary>
    WaterForOthers,
    /// <summary>The eater stands further away than the server's feedRange.</summary>
    TooFar,
    /// <summary>Another mod refused it, or the request couldn't be read.</summary>
    Other,
}

/// <summary>The mod's own words for each <see cref="MealRefusal"/>. A mod with its own texts can translate the enum
/// itself instead.</summary>
public static class MealRefusals
{
    public static string Describe(MealRefusal refusal) => refusal switch
    {
        MealRefusal.NotInWorld => "Not in the world yet.",
        MealRefusal.FeederDead => "The dead don't eat.",
        MealRefusal.EaterDead => "They are past eating.",
        MealRefusal.TooSoon => "Still chewing.",
        MealRefusal.NotFood => "That can't be eaten or drunk.",
        MealRefusal.NotCarried => "You have none of that left.",
        MealRefusal.NoWater => "There is no water here to drink.",
        MealRefusal.WaterForOthers => "Only you can drink the water you are in.",
        MealRefusal.TooFar => "They are too far away.",
        MealRefusal.Other => "That didn't work.",
        _ => throw new ArgumentOutOfRangeException(nameof(refusal), refusal, "Not a meal refusal."),
    };
}
