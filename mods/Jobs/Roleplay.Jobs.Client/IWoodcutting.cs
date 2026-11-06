using OblivionMP.SDK.Common.Foliage;
using ReadyM.Api.Idents;
using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Client;

/// <summary>
/// Woodcutting as the local player sees it, for a mod that draws its own interface (a CEF page, a widget): every hit
/// the server counts, every share of wood, and every tree felled or grown back while this player is in the world.
/// </summary>
/// <remarks>
/// <para>Read only: the server decides everything, and a battle axe's power attack on a tree is the chop. A tree is a
/// foliage instance, not a game reference, so it has no ref id: it is named by its <see cref="FoliageId"/>, its mesh and
/// where it stands, which is the same on every client and on the server.</para>
/// <para>A felling is also a networked event, <see cref="TreeFelledEvent"/>: a mod that wants to refuse one (server,
/// <c>Stage.Validate</c>) or react to one on any machine (<c>Stage.Observe</c>) handles that instead.</para>
/// <para>Set <see cref="ShowMessages"/> to false to take over from the messages Jobs shows in the top left.</para>
/// <para>Events are raised on the game thread, from the SDK's update, like the rest of the SDK. A mod that uses this
/// lists <c>Roleplay.Jobs</c> among its manifest's dependencies.</para>
/// </remarks>
/// <example>
/// <code>
/// woodcutting.ShowMessages = false;
/// woodcutting.HitCounted += hit => page.Send("progress", hit.Hits, hit.HitsNeeded);
/// woodcutting.TreeFelled += felled => { if (felled.ByLocalPlayer) page.Send("timber", felled.Kind); };
/// </code>
/// </example>
public interface IWoodcutting
{
    /// <summary>Whether Jobs shows its own messages in the top left. True until a mod says otherwise.</summary>
    bool ShowMessages { get; set; }

    /// <summary>Whether trees can only be felled in a lumber camp on this server.</summary>
    bool LumberCampsOnly { get; }

    /// <summary>The lumber camps, when <see cref="LumberCampsOnly"/>; empty otherwise.</summary>
    IReadOnlyList<MarkedArea> LumberCamps { get; }

    /// <summary>The server counted a hit of this player's on a tree.</summary>
    event Action<ChopHit>? HitCounted;

    /// <summary>A tree this player hit came down (the message lists their share), or the server would not count a
    /// hit or let the tree come down (the message says why).</summary>
    event Action<ChopEnd>? ChopEnded;

    /// <summary>A tree came down near this player, by anyone's axe, this player's included. Not raised for trees
    /// already down on joining.</summary>
    event Action<FelledTree>? TreeFelled;

    /// <summary>A felled tree grew back.</summary>
    event Action<FoliageId>? TreeGrewBack;
}

/// <summary>A hit the server counted.</summary>
/// <param name="Tree">The tree.</param>
/// <param name="Kind">The kind of tree as the server's config names it, such as "Oak"; empty for a tree of no listed kind.</param>
/// <param name="Hits">Hits on it so far, everyone's.</param>
/// <param name="HitsNeeded">Hits that fell it, with this player's axe.</param>
public readonly record struct ChopHit(FoliageId Tree, string Kind, int Hits, int HitsNeeded);

/// <summary>How a chop turned out, and the message Jobs shows for it.</summary>
public readonly record struct ChopEnd(ChopEndReason Reason, string Message);

public enum ChopEndReason
{
    /// <summary>A tree this player hit came down; the message lists what they got.</summary>
    Felled,

    /// <summary>The server would not count the hit, or the tree would not come down; the message says why.</summary>
    Refused,
}

/// <summary>A tree that came down.</summary>
/// <param name="Kind">The kind of tree as the server's config names it; empty for a tree of no listed kind.</param>
/// <param name="Feller">Whose hit felled it; everyone who hit it shares the wood.</param>
/// <param name="ByLocalPlayer">Whether this player's hit felled it.</param>
public readonly record struct FelledTree(FoliageId Tree, string Kind, float Scale, PlayerId Feller, bool ByLocalPlayer);
