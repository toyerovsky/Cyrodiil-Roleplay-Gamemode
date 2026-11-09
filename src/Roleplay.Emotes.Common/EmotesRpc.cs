using ReadyM.SDK.Attributes;

namespace Roleplay.Emotes.Common;

// The server decides who plays which emote. A client asks for one by id, the server tells that client to play it,
// and for a sequence, which the game does not send round as it does a montage, tells the others to play it on that
// player's character too.
[RpcContracts]
public static partial class EmotesRpc
{
    // The character is in the world. Only a ready client is sent emotes, its own or others'.
    [ClientToServer]
    public static partial void EmotesReady();

    [ClientToServer]
    public static partial void AskEmote(string id);

    [ClientToServer]
    public static partial void AskStopEmote();

    // firstPersonAnimation empty for none. seconds above 0 stops it after that long, 0 lets it play out.
    [ServerToClient]
    public static partial void PlayEmote(string animation, string firstPersonAnimation, float playRate, float seconds);

    // Another player's emote, a sequence, on their character as this client draws it. player: their PlayerId's raw value.
    [ServerToClient]
    public static partial void PlayEmoteOn(int player, string animation, float playRate);

    [ServerToClient]
    public static partial void StopEmote();
}
