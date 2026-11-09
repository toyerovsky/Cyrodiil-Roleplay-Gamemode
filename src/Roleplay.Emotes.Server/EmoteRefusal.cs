namespace Roleplay.Emotes.Server;

/// <summary>Why <see cref="IEmotes.Play"/> played nothing.</summary>
public enum EmoteRefusal
{
    /// <summary>No emote has that id, or it was taken off since the player saw it.</summary>
    Unknown = 1,

    /// <summary>The player's client has not said its character is in the world.</summary>
    NotInWorld = 2,
}
