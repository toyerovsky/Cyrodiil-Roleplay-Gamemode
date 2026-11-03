using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Server.Exchange;

internal sealed class ExchangeSide(OnlinePlayer player)
{
    public OnlinePlayer Player { get; } = player;

    public int Gold { get; set; }

    public bool Confirmed { get; set; }

    // The gold the window last showed them carrying. A change is sent again.
    public int SeenCarried { get; set; } = -1;
}
