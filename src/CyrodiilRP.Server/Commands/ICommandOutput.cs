using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Server.Commands;

internal interface ICommandOutput
{
    void Reply(OnlinePlayer to, Phrase text);

    void Refuse(OnlinePlayer to, Phrase text);
}
