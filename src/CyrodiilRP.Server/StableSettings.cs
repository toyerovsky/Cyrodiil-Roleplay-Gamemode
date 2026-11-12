using CyrodiilRP.Data;

namespace CyrodiilRP.Server;

// The gold each horse costs at a stablemaster. By default what the game's own stables ask for it.
internal sealed class StableSettings
{
    public int Paint { get; set; } = 500;

    public int Bay { get; set; } = 1000;

    public int Chestnut { get; set; } = 2500;

    public int White { get; set; } = 4000;

    public int Black { get; set; } = 5000;

    public int FindPrice(HorseKind horse) => horse switch
    {
        HorseKind.Paint => Paint,
        HorseKind.Bay => Bay,
        HorseKind.Chestnut => Chestnut,
        HorseKind.White => White,
        HorseKind.Black => Black,
    };

    // 0 gives the horse away. Below it the config is wrong, and the mod does not start.
    public void RequireValid()
    {
        foreach (var horse in Enum.GetValues<HorseKind>())
        {
            if (FindPrice(horse) < 0)
                throw new InvalidOperationException($"server/config.json: the price of the {horse} horse under stable is {FindPrice(horse)}, below 0.");
        }
    }
}
