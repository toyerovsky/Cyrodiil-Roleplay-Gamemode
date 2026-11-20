namespace CyrodiilRP.Common.Traders;

// Marks the mode's traders, bankers, stablemasters, mentors, registrars and auctioneers among the people of a cell, as a spawner's ref id marks its
// creatures: the server stands them with it, every client finds them by it and labels them. Above every ref id the game
// gives.
public static class TraderOrigin
{
    public const uint SpawnerRefId = 0xFFFFFFFD;

    public const uint BankerSpawnerRefId = 0xFFFFFFFC;

    public const uint StablemasterSpawnerRefId = 0xFFFFFFFB;

    public const uint WarriorMentorSpawnerRefId = 0xFFFFFFF9;

    public const uint MageMentorSpawnerRefId = 0xFFFFFFF8;

    public const uint SupportMentorSpawnerRefId = 0xFFFFFFF7;

    public const uint RegistrarSpawnerRefId = 0xFFFFFFF6;

    public const uint AuctioneerSpawnerRefId = 0xFFFFFFF5;
}
