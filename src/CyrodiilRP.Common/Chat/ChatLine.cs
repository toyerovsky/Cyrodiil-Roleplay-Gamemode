using LiteNetLib.Utils;
using ReadyM.Api.Multiplayer.Generators;

namespace CyrodiilRP.Common.Chat;

[DeriveINetSerializable]
public partial struct ChatLine : INetSerializable
{
    public ChatLineKind Kind;

    // The whole line, written in the reader's language by the server.
    public string Text;

    public static ChatLine Create(ChatLineKind kind, string text) => new() { Kind = kind, Text = text };
}
