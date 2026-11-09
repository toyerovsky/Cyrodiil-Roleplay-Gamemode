using ReadyM.Api.Idents;

namespace Roleplay.Emotes.Server;

/// <summary>
/// The emotes of the server, for the mods that depend on Roleplay.Emotes: register yours, list them, play one on a
/// player. The game's own come registered (<see cref="VanillaEmotes"/>).
/// </summary>
/// <remarks>Update thread only, like the rest of the SDK.</remarks>
/// <example>
/// <code>
/// // In a [Service] of a mod whose manifest depends on Roleplay.Emotes, from its Start.
/// emotes.Register(new Emote("mymod.jig", "Jig", "/Game/MyMod/Animations/AM_Jig.AM_Jig", AnimationKind.Montage)
/// {
///     Order = 500,
///     Translations = new Dictionary&lt;string, string&gt; { ["pl"] = "Zatańcz gigę" },
/// });
/// </code>
/// </example>
public interface IEmotes
{
    /// <summary>Every emote, in the wheel's order.</summary>
    IReadOnlyList<Emote> All { get; }

    /// <summary>Adds an emote. An id taken already, or an emote with no name or a path that is no object path, throws.</summary>
    void Register(Emote emote);

    /// <summary>Takes an emote off. False when none has that id.</summary>
    bool Unregister(string id);

    /// <summary>Plays the emote on the player's character, for everyone around to see.</summary>
    /// <returns>Null when it was sent to be played, otherwise why not.</returns>
    EmoteRefusal? Play(PlayerId player, string id);

    /// <summary>Stops whatever the player's character is playing.</summary>
    void Stop(PlayerId player);
}
