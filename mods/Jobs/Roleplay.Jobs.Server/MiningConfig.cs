namespace Roleplay.Jobs.Server;

public sealed class MiningConfig
{
    // Mining is hitting: a warhammer's power attack that lands on an ore node, or on rock inside a quarry, counts. The
    // warhammers that mine, by form id, each with a tier by its material, as the axes for trees: against a node whose kind
    // has a higher tier it takes TierPenalty times the hits for every tier it falls short, against a lower one TierBonus
    // times for every tier above (never under 1 hit). Empty lets any weapon's power attack mine, at the plain number of
    // hits. The server takes no two hits from one player closer than MinSecondsBetweenHits.
    public List<Hammer> Hammers { get; set; } =
    [
        Hammer("0x00019172", "Iron Warhammer", 1),
        Hammer("0x000229BE", "Steel Warhammer", 2),
        Hammer("0x0003A85E", "Fine Steel Warhammer", 2),
        Hammer("0x00025225", "Silver Warhammer", 2),
        Hammer("0x00035DD5", "Dwarven Warhammer", 3),
        Hammer("0x00035E69", "Elven Warhammer", 3),
        Hammer("0x00035E62", "Glass Warhammer", 4),
        Hammer("0x00035E71", "Ebony Warhammer", 4),
        Hammer("0x00035E79", "Daedric Warhammer", 5),
    ];
    public float TierPenalty { get; set; } = 2f;
    public float TierBonus { get; set; } = 0.85f;
    public float MinSecondsBetweenHits { get; set; } = 1f;

    // Durability every counted hit takes off the warhammer, as the game counts it. 0 turns it off.
    public float WearPerHit { get; set; } = 2f;

    // How far a player may stand from what they hit, in centimeters: the hammer's reach, with room for a big swing.
    public float Reach { get; set; } = 400f;

    // A hit counts on a node when it lands within HitRadius centimeters (times the node's scale) of the node's origin
    // on the ground plane. Its hits add up for everyone who strikes it, and are forgotten NodeMemorySeconds after the
    // last one; when it is spent, what it gives is shared by those hits (see Kinds).
    public float HitRadius { get; set; } = 200f;
    public float NodeMemorySeconds { get; set; } = 120f;

    // Quarries: rock within QuarryRadius centimeters of a quarry's middle can be quarried, every QuarryHitsPerYield
    // counted hits giving QuarryDrops (rolled as a node's drops, at scale 1). Quarries come from Quarries below and, with
    // QuarriesFromMarkers, from every map marker whose name holds QuarryMarkerName in the plugins the server hands its
    // players (PluginFolders and MarkerWorldspaces at the top). Rock is foliage whose mesh name holds one of QuarryMeshes and none of
    // ExcludedQuarryMeshes, ignoring case; with QuarryLandscape the bare ground inside a quarry counts too. A quarry face
    // does not run out.
    public bool QuarriesFromMarkers { get; set; } = true;
    public string QuarryMarkerName { get; set; } = "Quarry";
    public float QuarryRadius { get; set; } = 2000f;
    public List<AreaConfig> Quarries { get; set; } = [];
    public List<string> QuarryMeshes { get; set; } = ["rock", "cliff", "boulder", "stone"];
    public List<string> ExcludedQuarryMeshes { get; set; } = ["pebble", "path", "decal", "tree", "bush", "shrub"];
    public bool QuarryLandscape { get; set; }
    public int QuarryHitsPerYield { get; set; } = 3;
    public List<OreDrop> QuarryDrops { get; set; } =
    [
        Drop("0x00025946", "Stone Brick", 0, 0.4f, 1f, 1, 1),
        Drop("0x00025947", "Stone Brick", 0, 0.4f, 1f, 1, 1),
        Drop("0x00025948", "Stone Brick", 0, 0.4f, 1f, 1, 1),
        Drop("0x00025949", "Stone Brick", 0, 0.4f, 1f, 1, 1),
        Drop("0x0002594A", "Stone Brick", 0, 0.4f, 1f, 1, 1),
        Drop("0x040014C0", "Iron Ore", 5, 0.03f, 1f, 1, 1),
    ];

    // Every client within ViewDistance centimeters sees a spent node burst into BurstPieces of ChipAssets, each
    // BurstScale of its model times the node's scale, flying out at BurstSpeed centimeters a second and lying where they
    // land for the rest of BurstSeconds.
    public float ViewDistance { get; set; } = 15000f;
    public int BurstPieces { get; set; } = 14;
    public float BurstSpeed { get; set; } = 500f;
    public float BurstSeconds { get; set; } = 3f;
    public float BurstScale { get; set; } = 0.9f;

    // Experimental: every counted hit throws ChipsPerHit chips off where it landed, one of ChipAssets each, ChipScale of
    // the model, flying away from the miner at ChipSpeed centimeters a second and lying where they land, at the miner's
    // feet, for the rest of ChipSeconds. Plain props of each client's own: nothing to pick up. Off with ExperimentalChips
    // false.
    public bool ExperimentalChips { get; set; }
    public int ChipsPerHit { get; set; } = 3;
    public float ChipSpeed { get; set; } = 450f;
    public float ChipSeconds { get; set; } = 1.5f;
    public float ChipScale { get; set; } = 0.8f;
    public List<string> ChipAssets { get; set; } =
    [
        StoneWallStone(1), StoneWallStone(2), StoneWallStone(3), StoneWallStone(4), StoneWallStone(5),
    ];

    // The kinds of node. Asset is what stands there (KotN's iron vein container, a remastered gold rock, the Shivering
    // Isles' madness ore deposit container), sized by Scale; Hits how many it takes, by tier as above; Tier how hard it
    // is, against the warhammer's. Every drop is rolled on its own: it drops at all with its Chance, Base of it for a
    // node of scale 1, times the node's scale, where a fraction is a chance of one more, kept between Min and Max. A
    // spent node's yield is shared by everyone who hit it, by their hits, most valuable first (Value, the item's gold
    // value as the game has it): whoever did most of the work gets the best of it. It grows back after RespawnMinutes.
    // Oblivion has no stone or ore item but nuggets: the stone bricks of its walls are the common drop.
    public List<OreKind> Kinds { get; set; } =
    [
        new()
        {
            Name = "Iron", Asset = Container("DL9IronVein01"), Hits = 6, Tier = 1, RespawnMinutes = 20f,
            Drops =
            [
                Drop("0x040014C0", "Iron Ore", 5, 1f, 2f, 1, 4),
                Drop("0x00025946", "Stone Brick", 0, 0.3f, 1f, 1, 2),
                Drop("0x00025947", "Stone Brick", 0, 0.3f, 1f, 1, 2),
                Drop("0x00025948", "Stone Brick", 0, 0.3f, 1f, 1, 2),
                Drop("0x00038B9C", "Flawed Topaz", 5, 0.05f, 1f, 1, 1),
                Drop("0x00038B9F", "Flawed Ruby", 10, 0.03f, 1f, 1, 1),
            ],
        },
        new()
        {
            Name = "Rich", Asset = DungeonRock("SM_goldrock02"), Hits = 8, Tier = 2, RespawnMinutes = 30f,
            Drops =
            [
                Drop("0x040014C0", "Iron Ore", 5, 1f, 1.5f, 1, 3),
                Drop("0x00049809", "Silver Nugget", 15, 0.5f, 1f, 1, 2),
                Drop("0x00049808", "Gold Nugget", 25, 0.2f, 1f, 1, 1),
                Drop("0x00025949", "Stone Brick", 0, 0.3f, 1f, 1, 2),
                Drop("0x0002594A", "Stone Brick", 0, 0.3f, 1f, 1, 2),
                Drop("0x00038BA4", "Emerald", 40, 0.04f, 1f, 1, 1),
                Drop("0x00038BA7", "Diamond", 50, 0.02f, 1f, 1, 1),
                Drop("0x00038BA9", "Flawless Diamond", 100, 0.005f, 1f, 1, 1),
            ],
        },
        new()
        {
            Name = "Madness", Asset = Container("SEMadnessRockDeposit"), Hits = 6, Tier = 3, RespawnMinutes = 30f,
            Drops =
            [
                Drop("0x00012D33", "Madness Ore", 20, 1f, 2f, 1, 3),
                Drop("0x00016568", "Amber", 10, 0.2f, 1f, 1, 1),
            ],
        },
    ];

    // Nodes standing on this server: a kind's Name, where its origin goes (centimeters, the Unreal world), its yaw
    // (degrees) and scale. Nodes placed in game (AllowNodePlacement) are kept in the save file instead.
    public List<OreNode> Nodes { get; set; } = [];

    // Lets every player place a node of the kind Shift+J picks 150 cm in front of them, or remove the placed node in front
    // of them, with J. For building a server's nodes; leave it off on a live server.
    public bool AllowNodePlacement { get; set; }

    private static OreDrop Drop(string item, string name, int value, float chance, float @base, int min, int max)
        => new() { Item = item, Name = name, Value = value, Chance = chance, Base = @base, Min = min, Max = max };

    private static string Container(string editorId) => $"/Game/Forms/worldobjects/container/{editorId}.{editorId}";

    // KotN's second vein shows the 2006 silver rock, grey and without collision in the remaster: a remastered gold rock
    private static string DungeonRock(string mesh) => $"/Game/Art/Environment/dungeons/misc/{mesh}.{mesh}";

    // The brick's mesh itself: its misc item form does not lead a prop to it
    private static string StoneWallStone(int number)
        => $"/Game/Art/Environment/Architecture/stonewall/SM_stonewallstone0{number}.SM_stonewallstone0{number}";

    private static Hammer Hammer(string item, string name, int tier) => new() { Item = item, Name = name, Tier = tier };
}

public sealed class Hammer
{
    public string Item { get; set; } = "";
    public string Name { get; set; } = "";
    public int Tier { get; set; } = 1;
}

public sealed class OreKind
{
    public string Name { get; set; } = "";
    public string Asset { get; set; } = "";
    public float Scale { get; set; } = 1f;
    public int Hits { get; set; } = 6;
    public int Tier { get; set; } = 1;
    public float RespawnMinutes { get; set; } = 20f;
    public List<OreDrop> Drops { get; set; } = [];
}

public sealed class OreDrop
{
    public string Item { get; set; } = "";
    public string Name { get; set; } = "";
    public int Value { get; set; }
    public float Chance { get; set; } = 1f;
    public float Base { get; set; } = 1f;
    public int Min { get; set; }
    public int Max { get; set; } = 100;
}

public sealed class OreNode
{
    public string Kind { get; set; } = "";
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float Yaw { get; set; }
    public float Scale { get; set; } = 1f;
}
