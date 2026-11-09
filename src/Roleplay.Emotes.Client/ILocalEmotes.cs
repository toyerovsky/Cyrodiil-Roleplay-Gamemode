namespace Roleplay.Emotes.Client;

/// <summary>Emotes from the client, for the client mods that depend on Roleplay.Emotes: a key or a page of their own
/// asks the server, which checks the id and plays it for everyone around.</summary>
public interface ILocalEmotes
{
    /// <summary>Asks for the emote with this id (an <c>Emote.Id</c> of the server).</summary>
    void Ask(string id);

    /// <summary>Asks to stop whatever the character is playing.</summary>
    void AskStop();
}
