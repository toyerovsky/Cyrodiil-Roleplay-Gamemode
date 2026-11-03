using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Tests.Fakes;

// What commands answered, by the keys of their texts.
internal sealed class RecordedOutput : ICommandOutput
{
    public List<string> Replies { get; } = [];

    public List<string> Refusals { get; } = [];

    public void Reply(OnlinePlayer to, Phrase text) => Replies.Add(text.Key);

    public void Refuse(OnlinePlayer to, Phrase text) => Refusals.Add(text.Key);
}
