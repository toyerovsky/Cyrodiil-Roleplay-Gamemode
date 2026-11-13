namespace Roleplay.Jobs.Server;

public sealed class DocksConfig
{
    // The docks: crates of CrateKind (a Carrying kind) come from inlets and go to outlets, deposits ZoneSize centimeters
    // on a side. An inlet keeps a pile of crates lying in it for a crowd of new players: a pyramid of touching crates,
    // InletRows by InletRows on the floor and one fewer each way on each of up to InletLayers layers (14 by default),
    // CrateGap centimeters between neighbours, each spot filled again RestockSeconds after its crate was taken, once the
    // crates under it are there. The pile is packed by the crate's size, which the first player allowed to set up the
    // docks measures in their game; until then it is a flat square CrateSpacing apart. A crate from an
    // inlet put down in an outlet is delivered: the dock foreman owes whoever carried it Wage gold, and it is cleared
    // away within ClearSeconds. Each deposit stands on a pallet built of PalletBoard (a TES form's Unreal asset or a
    // StaticMesh): runners under a deck of boards, the pile's footprint and PalletMargin centimeters more each way. The
    // board's size is measured as the crate's is; the pile stands on the pallet. Empty for no pallet. PalletWoodShare is
    // how much of the board's measured height, from the top, is wood (the rest is nails sticking out below);
    // PalletRunnerInset how far in from each side the outer runners lie, as a share of the pallet's width, so they are
    // under the deck boards' nails.
    public string CrateKind { get; set; } = "Crate";
    public float ZoneSize { get; set; } = 300f;
    public int InletRows { get; set; } = 3;
    public int InletLayers { get; set; } = 3;
    public float CrateGap { get; set; } = 2f;
    public float CrateSpacing { get; set; } = 90f;
    public float RestockSeconds { get; set; } = 10f;
    public float ClearSeconds { get; set; } = 10f;
    public int Wage { get; set; } = 1;
    public string PalletBoard { get; set; } = "/Game/Forms/worldobjects/static/LooseBoard01.LooseBoard01";
    public float PalletMargin { get; set; } = 4f;
    public float PalletWoodShare { get; set; } = 0.5f;
    public float PalletRunnerInset { get; set; } = 0.13f;

    // The foremen players talk to with the interaction key, in a window like a shop's: each pays the wages owed
    // (PaysWages), sells what is in Sells and buys what is in Buys, at Price gold each. An item is a form id, or
    // "custom:" and the key of one of Woodcutting.Items; Icon the game's editor id whose icon the window shows for it,
    // from those the shops have (empty for none). They are made from BaseNpc, a person with no services of their own. The first three are placed
    // with Numpad 2, 3 and 4.
    public string BaseNpc { get; set; } = "0x000025FA";
    public List<ForemanRole> Foremen { get; set; } =
    [
        new() { Name = "Dock Foreman", PaysWages = true },
        new()
        {
            Name = "Lumber Camp Leader",
            Sells = [new ForemanWare { Item = "custom:jobs.lumber-axe", Name = "Lumber Axe", Price = 50, Icon = "WeapIronWarAxe" }],
            Buys = [new ForemanWare { Item = "custom:jobs.firewood", Name = "Firewood", Price = 1 }],
        },
        new()
        {
            Name = "Quarry Foreman",
            Sells = [new ForemanWare { Item = "0x00019172", Name = "Iron Warhammer", Price = 150, Icon = "WeapIronMace" }],
            Buys =
            [
                new ForemanWare { Item = "0x00025946", Name = "Stone Brick", Price = 1 },
                new ForemanWare { Item = "0x00025947", Name = "Stone Brick", Price = 1 },
            ],
        },
    ];

    // How far in front of a player a foreman or deposit counts for talking to and for removing, in centimeters.
    public float Reach { get; set; } = 300f;

    // Lets every player place foremen and deposits with the placement mode (Numpad Decimal, the Jobs category) and
    // remove the nearest (Numpad 0): for setting up a server, off for play.
    public bool AllowSetup { get; set; } = true;

    // Where the deposits and foremen, and the wages owed, are kept, next to the server DLL.
    public string SaveFile { get; set; } = "docks.json";
    public string WagesFile { get; set; } = "wages.json";
}
