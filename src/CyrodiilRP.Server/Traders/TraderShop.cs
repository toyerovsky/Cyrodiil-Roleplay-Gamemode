namespace CyrodiilRP.Server.Traders;

internal sealed record TraderShop(string Name, IReadOnlyList<Ware> Wares)
{
    public Ware? Find(string id) => Wares.FirstOrDefault(ware => ware.Id == id);
}
