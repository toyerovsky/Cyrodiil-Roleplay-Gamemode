namespace Roleplay.Jobs.Server;

public sealed class WoodcuttingConfig : IHitRules
{
    // A tree is foliage whose mesh name (the last part of its path) contains one of TreeMeshes and none of
    // ExcludedMeshes, ignoring case. Branch and Bark keep out the fallen branches (SM_Branches_TreeBark_*).
    public List<string> TreeMeshes { get; set; } = ["tree"];
    public List<string> ExcludedMeshes { get; set; } = ["shrub", "bush", "stump", "branch", "bark"];

    // Woodcutting is hitting: an attack with one of Axes that lands on a tree damages it, from anyone. A normal attack
    // does NormalDamage and always lands; a power attack does PowerDamage, but often makes no impact on a trunk
    // standing up (the game's hit box), so it is the bonus, not the way. A hit counts within HitRadius centimeters of
    // the trunk's middle on the ground plane, from a player standing within Reach of where it landed; the server takes
    // no normal hit from one player sooner than MinSecondsBetweenHits after their last, and no power hit sooner than
    // MinSecondsBetweenPowerHits. A tree's damage adds up for everyone who strikes it, and is forgotten
    // TreeMemorySeconds after the last hit; when it comes down, what it gives is shared by that damage (see Drops).
    public float Reach { get; set; } = 400f;
    public float HitRadius { get; set; } = 150f;
    public int NormalDamage { get; set; } = 1;
    public int PowerDamage { get; set; } = 5;

    // Strength adds to a hit: a normal attack does NormalStrengthBonus more for every NormalStrengthStep points of the
    // character's Strength above NormalStrengthFrom (1 for every 20 above 40: 2 at Strength 60), a power attack
    // PowerStrengthBonus more for every PowerStrengthStep points above PowerStrengthFrom (2 for every 10 above 40: 9 at
    // 60). Strength as the character has it now, so a Fortify Strength potion helps and hunger's drains hurt. A step of
    // 0 turns the bonus off.
    public int NormalStrengthBonus { get; set; } = 1;
    public int NormalStrengthStep { get; set; } = 20;
    public int NormalStrengthFrom { get; set; } = 40;
    public int PowerStrengthBonus { get; set; } = 2;
    public int PowerStrengthStep { get; set; } = 10;
    public int PowerStrengthFrom { get; set; } = 40;
    public float MinSecondsBetweenHits { get; set; } = 0.3f;
    public float MinSecondsBetweenPowerHits { get; set; } = 0.8f;
    public float TreeMemorySeconds { get; set; } = 120f;

    // A tree comes down at its health: BaseHealth for a trunk as thick as a scale 1 oak, more for a thicker one (the
    // tree's Girth times its scale), rounded and kept between MinHealth and MaxHealth. 20 is four power attacks or
    // twenty normal ones.
    public int BaseHealth { get; set; } = 20;
    public int MinHealth { get; set; } = 10;
    public int MaxHealth { get; set; } = 50;

    // An axe of a higher tier than the tree's wood hits harder: NormalTierBonus more for a normal attack and
    // PowerTierBonus more for a power attack, for every tier above (an ebony axe, tier 4, on pine, tier 1: +3 and +6).
    // The tree's health stays what it is. One of a lower tier makes the tree TierPenalty times as tough for every tier
    // it falls short (2: an iron axe on oak, two tiers short, needs four times the damage), past MaxHealth; 1 turns
    // that off.
    public int NormalTierBonus { get; set; } = 1;
    public int PowerTierBonus { get; set; } = 2;
    public float TierPenalty { get; set; } = 2f;

    // Durability a hit takes off the axe for every point of damage it does, as the game counts it (an iron battle axe
    // has a few hundred, a hammer repairs it): 0.4 is 2 for a power attack. 0 turns it off.
    public float WearPerDamage { get; set; } = 0.4f;

    // Every counted hit throws ChipsPerHit pieces of ChipAssets off where it landed, one of them each, ChipScale of the
    // model, flying back towards the chopper at ChipSpeed centimeters a second and lying where they land, at the
    // chopper's feet, for the rest of ChipSeconds, for every player within FallViewDistance. Plain props of each
    // client's own: nothing to pick up. 0 turns them off. SM_gnarlbark is the Gnarl Bark ingredient's own model.
    public int ChipsPerHit { get; set; } = 3;
    public float ChipSpeed { get; set; } = 350f;
    public float ChipSeconds { get; set; } = 1.5f;
    public float ChipScale { get; set; } = 1f;
    public List<string> ChipAssets { get; set; } = ["/Game/Art/Environment/clutter/SM_gnarlbark.SM_gnarlbark"];

    // Lumber camps: with LumberCampsOnly, a tree can only be felled within LumberCampRadius centimeters of a lumber
    // camp's middle; without, any tree anywhere. Camps come from LumberCamps below and, with LumberCampsFromMarkers,
    // from every map marker whose name holds LumberCampMarkerName, read as the quarries' are (PluginFolders and
    // MarkerWorldspaces at the top).
    public bool LumberCampsOnly { get; set; }
    public bool LumberCampsFromMarkers { get; set; } = true;
    public string LumberCampMarkerName { get; set; } = "Lumber";
    public float LumberCampRadius { get; set; } = 3000f;
    public List<AreaConfig> LumberCamps { get; set; } = [];

    // The axes that chop, by form id, each with a tier by its material: against a tree whose kind has a higher Tier it
    // makes it tougher (TierPenalty), a lower one hit harder (NormalTierBonus, PowerTierBonus). The player needs one equipped; the best one equipped
    // counts. Empty lets any weapon chop, at the plain health. Durability is the axe's full durability, as the game's
    // data has it.
    //
    // A worn axe bites less: below DullBelow of its full durability a hit may glance off and do nothing, a chance
    // rising evenly from none at DullBelow to MaxGlanceChance just before the axe breaks (0.5 and 0.5: at a quarter of
    // its durability, one hit in four). A broken axe does nothing at all. DullBelow 0 turns glancing off.
    public float DullBelow { get; set; } = 0.5f;
    public float MaxGlanceChance { get; set; } = 0.5f;
    public List<Axe> Axes { get; set; } =
    [
        Axe("0x00000D7F", "Iron Battle Axe", 1, 168),
        Axe("0x0003A85F", "Fine Iron Battle Axe", 1, 185),
        Axe("0x000229B6", "Steel Battle Axe", 2, 224),
        Axe("0x0003A856", "Fine Steel Battle Axe", 2, 246),
        Axe("0x00025221", "Silver Battle Axe", 2, 288),
        Axe("0x00035DCD", "Dwarven Battle Axe", 3, 360),
        Axe("0x00035E67", "Elven Battle Axe", 3, 440),
        Axe("0x00035E60", "Glass Battle Axe", 4, 528),
        Axe("0x00035E6F", "Ebony Battle Axe", 4, 624),
        Axe("0x00035E77", "Daedric Battle Axe", 5, 728),
    ];

    // What a felled tree gives: every entry is rolled on its own. An entry drops at all with its Chance (1 always).
    // Its count is Base for a scale 1 tree, times the tree's scale to the ScaleExponent, times its kind's Wood when
    // ScalesWithWood is set. A tree is its model scaled the same on every axis, so its timber (basal area times height)
    // grows with the cube of its scale: ScaleExponent 3 is that, 2 counts the trunk's cross-section alone and keeps a
    // giant from giving tenfold, 0 gives the same from any tree. A fraction is a chance of one more: 4.3 gives 5 three
    // times in ten. The count is kept between Min and Max. Kinds, when not empty, limits the entry to trees whose mesh
    // name holds one of them, as Kinds below match. A felled tree's yield is shared by everyone who hit it, by their
    // hits, most valuable first (Value, the item's gold value as the game has it): whoever did most of the work gets
    // the best of it. Oblivion has no log item, so the wood is a stand-in until the mod ships one.
    public List<TreeDrop> Drops { get; set; } =
    [
        new() { Item = "0x00044E5E", Name = "Gnarl Bark", Value = 1, Base = 3f, ScaleExponent = 2f, ScalesWithWood = true, Min = 1, Max = 20 },
    ];

    // Real minutes until a felled tree grows back.
    public float RespawnMinutes { get; set; } = 30f;

    // What a felled tree leaves until it grows back, sized to the tree. A TES form's Unreal asset or a StaticMesh.
    // The stump stands where the trunk did, StumpSink centimeters into the ground and turned at random. Stump01 is the
    // only plain stump the game has: a chopping block, flat on top like a fresh cut.
    public string Stump { get; set; } = "/Game/Forms/worldobjects/static/Stump01.Stump01";
    public float StumpSink { get; set; } = 20f;

    // A felled tree falls on every client within FallViewDistance centimeters of it: a copy of the tree tips away from the
    // chopper, faster as it goes, until FallDegrees off upright after FallSeconds (longer for a bigger tree, by the
    // square root of its scale, as a falling pole), lies there FallLieSeconds, then sinks FallSinkDepth centimeters (times
    // its scale) into the ground over FallSinkSeconds and is gone. Nothing collides with it. On a slope it can cut into
    // the hill. TreesFall false makes a felled tree just vanish.
    public bool TreesFall { get; set; } = true;
    public float FallSeconds { get; set; } = 2.5f;
    public float FallDegrees { get; set; } = 85f;
    public float FallLieSeconds { get; set; } = 2f;
    public float FallSinkSeconds { get; set; } = 6f;
    public float FallSinkDepth { get; set; } = 1500f;
    public float FallViewDistance { get; set; } = 15000f;

    // What sets one kind of tree apart, each at its model's scale 1: how wide its stump is next to the chopping block's
    // (a birch's trunk is thin for its height), how much wood it holds and how thick its trunk is, both next to an
    // oak's, and its wood's tier by its real hardness (Janka), against the axe's:
    // softwoods, poplar and aspen 1, birch and holly 2, oak, maple and yew 3. The first kind whose Match is in the tree's
    // mesh name (so ScotchPine before Pine, WillowOak an oak) is the tree's, else DefaultKind. Guesses until tuned in game.
    public List<TreeKind> Kinds { get; set; } =
    [
        Kind("ScotchPine", stumpScale: 0.9f, wood: 0.9f, girth: 0.9f, tier: 1),
        Kind("Hemlock", stumpScale: 1f, wood: 1f, girth: 1f, tier: 1),
        Kind("Redwood", stumpScale: 1.3f, wood: 2.5f, girth: 1.6f, tier: 1),
        Kind("Pine", stumpScale: 0.8f, wood: 0.8f, girth: 0.8f, tier: 1),
        Kind("Poplar", stumpScale: 0.9f, wood: 0.9f, girth: 0.9f, tier: 1),
        Kind("Aspen", stumpScale: 0.6f, wood: 0.5f, girth: 0.6f, tier: 1),
        Kind("Birch", stumpScale: 0.5f, wood: 0.4f, girth: 0.5f, tier: 2),
        Kind("Holly", stumpScale: 0.6f, wood: 0.5f, girth: 0.6f, tier: 2),
        Kind("Maple", stumpScale: 1f, wood: 1f, girth: 1f, tier: 3),
        Kind("Yew", stumpScale: 1f, wood: 0.8f, girth: 1f, tier: 3),
        Kind("Oak", stumpScale: 1f, wood: 1f, girth: 1f, tier: 3),
    ];
    public TreeKind DefaultKind { get; set; } = Kind("", stumpScale: 1f, wood: 1f, girth: 1f, tier: 1);

    private static Axe Axe(string item, string name, int tier, int durability)
        => new() { Item = item, Name = name, Tier = tier, Durability = durability };

    private static TreeKind Kind(string match, float stumpScale, float wood, float girth, int tier)
        => new() { Match = match, StumpScale = stumpScale, Wood = wood, Girth = girth, Tier = tier };
}
