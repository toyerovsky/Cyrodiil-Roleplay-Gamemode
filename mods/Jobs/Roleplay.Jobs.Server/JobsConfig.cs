using ReadyM.SDK.Attributes;

namespace Roleplay.Jobs.Server;

// server/config.json. Every key is optional; a missing one keeps the default below.
[ModConfig]
public sealed partial class JobsConfig
{
    public WoodcuttingConfig Woodcutting { get; set; } = new();

    public MiningConfig Mining { get; set; } = new();

    // Where felled trees and when they grow back are kept, next to the server DLL.
    public string SaveFile { get; set; } = "felled-trees.json";

    // Where ore nodes placed in game, and the mined ones with when they grow back, are kept, next to the server DLL.
    public string NodeSaveFile { get; set; } = "mining-nodes.json";
}

public sealed class WoodcuttingConfig
{
    // A tree is foliage whose mesh name (the last part of its path) contains one of TreeMeshes and none of
    // ExcludedMeshes, ignoring case. Branch and Bark keep out the fallen branches (SM_Branches_TreeBark_*).
    public List<string> TreeMeshes { get; set; } = ["tree"];
    public List<string> ExcludedMeshes { get; set; } = ["shrub", "bush", "stump", "branch", "bark"];

    // How far from the trunk a player can chop, in centimeters, and how far off straight ahead the trunk may be, in
    // degrees. The character's body has to face the tree: the swing goes where the body faces.
    public float Reach { get; set; } = 250f;
    public float FacingDegrees { get; set; } = 60f;

    // A tree comes down after a number of swings that land on its trunk, within HitRadius of its middle: BaseHits for a
    // trunk as thick as a scale 1 oak, more for a thicker one (the tree's Girth times its scale), rounded and kept
    // between MinHits and MaxHits. The player swings once every SwingSeconds and gives up after SwingsPerHit swings for
    // every hit needed. 1.8 starts the next swing over the recovery of the last, which looks smoother in game than
    // letting it finish. The server takes no two hits closer than MinSecondsBetweenHits apart, so a client cannot fell
    // a tree faster than its swings.
    public int BaseHits { get; set; } = 4;
    public int MinHits { get; set; } = 2;
    public int MaxHits { get; set; } = 10;

    // An axe of a lower tier than the tree's wood takes TierPenalty times the hits for every tier it falls short (2: an
    // iron axe on oak, two tiers short, takes four times as many), past MaxHits; one of a higher tier takes TierBonus
    // times the hits for every tier above (never under MinHits). 1 for either turns it off.
    public float TierPenalty { get; set; } = 2f;
    public float TierBonus { get; set; } = 0.85f;

    // Durability every counted hit takes off the axe, as the game counts it (an iron battle axe has a few hundred, a
    // hammer repairs it). 0 turns it off.
    public float WearPerHit { get; set; } = 2f;
    public int SwingsPerHit { get; set; } = 2;
    public float SwingSeconds { get; set; } = 1.8f;
    public float HitRadius { get; set; } = 120f;
    public float MinSecondsBetweenHits { get; set; } = 1f;

    // The swing: a montage (AM_), which replicates by itself, or an animation sequence (A_), which the chopper's client
    // sends to the players around. FirstPersonAnimation plays with it on the first-person mesh, which has its own _FP
    // ones; "" for none. The warhammer's first combo swing is on trial; the battle axe's third and the claymore power
    // attack are the alternatives (see config.json).
    public string Animation { get; set; } =
        "/Game/Art/Animation/Humanoid/ThirdPerson/TwoHand/Combat/Attacks/Warhammer/A_Humanoid_Warhammer_Attack_A_v0.A_Humanoid_Warhammer_Attack_A_v0";
    public string FirstPersonAnimation { get; set; } =
        "/Game/Art/Animation/Humanoid/FirstPerson/TwoHand/Combat/Attacks/Warhammer/A_Humanoid_Warhammer_Attack_A_FP.A_Humanoid_Warhammer_Attack_A_FP";
    public float PlayRate { get; set; } = 1f;

    // The axes that chop, by form id, each with a tier by its material: against a tree whose kind has a higher Tier it
    // takes more hits (TierPenalty), a lower one fewer (TierBonus). The player needs one equipped; the best one equipped
    // counts. Empty lets anyone chop bare-handed, any tree, at the plain number of hits.
    public List<Axe> Axes { get; set; } =
    [
        Axe("0x00000D7F", "Iron Battle Axe", 1),
        Axe("0x0003A85F", "Fine Iron Battle Axe", 1),
        Axe("0x000229B6", "Steel Battle Axe", 2),
        Axe("0x0003A856", "Fine Steel Battle Axe", 2),
        Axe("0x00025221", "Silver Battle Axe", 2),
        Axe("0x00035DCD", "Dwarven Battle Axe", 3),
        Axe("0x00035E67", "Elven Battle Axe", 3),
        Axe("0x00035E60", "Glass Battle Axe", 4),
        Axe("0x00035E6F", "Ebony Battle Axe", 4),
        Axe("0x00035E77", "Daedric Battle Axe", 5),
    ];

    // What a felled tree gives: every entry is rolled on its own. An entry drops at all with its Chance (1 always).
    // Its count is Base for a scale 1 tree, times the tree's scale to the ScaleExponent, times its kind's Wood when
    // ScalesWithWood is set. A tree is its model scaled the same on every axis, so its timber (basal area times height)
    // grows with the cube of its scale: ScaleExponent 3 is that, 2 counts the trunk's cross-section alone and keeps a
    // giant from giving tenfold, 0 gives the same from any tree. A fraction is a chance of one more: 4.3 gives 5 three
    // times in ten. The count is kept between Min and Max. Kinds, when not empty, limits the entry to trees whose mesh
    // name holds one of them, as Kinds below match.
    // Oblivion has no log item, so the wood is a stand-in until the mod ships one.
    public List<TreeDrop> Drops { get; set; } =
    [
        new() { Item = "0x00044E5E", Name = "Gnarl Bark", Base = 3f, ScaleExponent = 2f, ScalesWithWood = true, Min = 1, Max = 20 },
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

    private static Axe Axe(string item, string name, int tier) => new() { Item = item, Name = name, Tier = tier };

    private static TreeKind Kind(string match, float stumpScale, float wood, float girth, int tier)
        => new() { Match = match, StumpScale = stumpScale, Wood = wood, Girth = girth, Tier = tier };
}

public sealed class Axe
{
    public string Item { get; set; } = "";
    public string Name { get; set; } = "";
    public int Tier { get; set; } = 1;
}

public sealed class TreeDrop
{
    public string Item { get; set; } = "";
    public string Name { get; set; } = "";
    public float Chance { get; set; } = 1f;
    public float Base { get; set; } = 1f;
    public float ScaleExponent { get; set; } = 0f;
    public bool ScalesWithWood { get; set; }
    public int Min { get; set; }
    public int Max { get; set; } = 100;
    public List<string> Kinds { get; set; } = [];
}

public sealed class TreeKind
{
    public string Match { get; set; } = "";
    public float StumpScale { get; set; } = 1f;
    public float Wood { get; set; } = 1f;
    public float Girth { get; set; } = 1f;
    public int Tier { get; set; } = 1;
}
