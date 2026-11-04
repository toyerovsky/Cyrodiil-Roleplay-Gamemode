using OblivionMp.Sdk.Common.Foliage;

namespace Roleplay.Jobs.Client;

/// <summary>
/// Woodcutting as the local player sees it, for a mod that draws its own interface (a CEF page, a widget): the chop
/// going on now, how far along it is, and every tree felled or grown back while this player is in the world.
/// </summary>
/// <remarks>
/// <para>Read only: the server decides everything, and the G key starts and stops a chop. A tree is a foliage instance,
/// not a game reference, so it has no ref id: it is named by its <see cref="FoliageId"/>, its mesh and where it stands,
/// which is the same on every client and on the server.</para>
/// <para>Set <see cref="ShowMessages"/> to false to take over from the messages Jobs shows in the top left.</para>
/// <para>Events are raised on the game thread, from the SDK's update, like the rest of the SDK. A mod that uses this
/// lists <c>Roleplay.Jobs</c> among its manifest's dependencies.</para>
/// </remarks>
/// <example>
/// <code>
/// woodcutting.ShowMessages = false;
/// woodcutting.HitLanded += chop => page.Send("progress", chop.HitsDone, chop.HitsNeeded);
/// woodcutting.TreeFelled += felled => { if (felled.ByLocalPlayer) page.Send("timber", felled.Kind); };
/// </code>
/// </example>
public interface IWoodcutting
{
    ChopPhase Phase { get; }

    /// <summary>The chop going on now, or null when <see cref="Phase"/> is not <see cref="ChopPhase.Swinging"/>.</summary>
    Chop? Current { get; }

    /// <summary>Whether Jobs shows its own messages in the top left. True until a mod says otherwise.</summary>
    bool ShowMessages { get; set; }

    /// <summary>The server accepted a chop and the swinging begins.</summary>
    event Action<Chop>? ChopStarted;

    /// <summary>The server counted a hit on the trunk.</summary>
    event Action<Chop>? HitLanded;

    /// <summary>The chop is over: the tree came down, the server refused it, or the player stopped.</summary>
    event Action<ChopEnd>? ChopEnded;

    /// <summary>A tree came down, by anyone's axe, this player's included. Not raised for trees already down on joining.</summary>
    event Action<FelledTree>? TreeFelled;

    /// <summary>A felled tree grew back.</summary>
    event Action<FoliageId>? TreeGrewBack;
}

public enum ChopPhase
{
    /// <summary>Not chopping.</summary>
    Idle,

    /// <summary>Asked the server to chop a tree; it has not answered yet.</summary>
    Asking,

    /// <summary>Swinging at a tree.</summary>
    Swinging,
}

/// <summary>A chop under way.</summary>
/// <param name="Tree">The tree.</param>
/// <param name="Kind">The kind of tree as the server's config names it, such as "Oak"; empty for a tree of no listed kind.</param>
/// <param name="WoodTier">How hard its wood is, 1 the softest.</param>
/// <param name="AxeTier">The tier of the axe it is chopped with, or null when the server needs no axe.</param>
/// <param name="Scale">The tree's own scale, 1 for its model as made.</param>
/// <param name="HitsDone">Hits the server has counted so far.</param>
/// <param name="HitsNeeded">Hits that fell it.</param>
/// <param name="SwingsDone">Swings taken so far, landed or not.</param>
/// <param name="MaxSwings">Swings the player gets before giving up.</param>
public sealed record Chop(FoliageId Tree, string Kind, int WoodTier, int? AxeTier, float Scale, int HitsDone, int HitsNeeded,
    int SwingsDone, int MaxSwings);

/// <summary>How a chop ended, and the message Jobs shows for it.</summary>
public readonly record struct ChopEnd(FoliageId Tree, ChopEndReason Reason, string Message);

public enum ChopEndReason
{
    /// <summary>The tree came down; the message lists what the player got.</summary>
    Felled,

    /// <summary>The server would not start or go on with it; the message says why.</summary>
    Refused,

    /// <summary>The player stopped: pressed G again, walked away, or ran out of swings.</summary>
    Stopped,
}

/// <summary>A tree that came down.</summary>
/// <param name="Kind">The kind of tree as the server's config names it; empty for a tree of no listed kind.</param>
/// <param name="ByLocalPlayer">Whether this player felled it.</param>
public readonly record struct FelledTree(FoliageId Tree, string Kind, float Scale, bool ByLocalPlayer);
