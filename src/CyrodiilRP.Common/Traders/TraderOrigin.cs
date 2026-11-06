namespace CyrodiilRP.Common.Traders;

// Marks the mode's traders and bankers among the people of a cell, as a spawner's ref id marks its creatures: the
// server stands them with it, every client finds them by it and labels them. Above every ref id the game gives.
public static class TraderOrigin
{
    public const uint SpawnerRefId = 0xFFFFFFFD;

    public const uint BankerSpawnerRefId = 0xFFFFFFFC;
}
