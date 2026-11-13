namespace Roleplay.Jobs.Server;

public sealed class CarryConfig
{
    // Carrying: CarryKey picks up the carryable in front of the player, within Reach centimeters of a spot a little
    // ahead of them, and puts down the one in hand. It hangs on Socket of the character's hand for everyone near,
    // weighs its kind's Weight on the carrier as a Burden, and is put down where they stand when they die or leave.
    public string CarryKey { get; set; } = "U";
    public float Reach { get; set; } = 150f;
    public string Socket { get; set; } = "Weapon_Socket";

    // A carrier holds their arms out with HoldAnimation, a sequence that plays on the upper body only, over and over
    // while they carry (HoldAnimationFirstPerson in first person view). Empty holds nothing.
    public string HoldAnimation { get; set; } =
        "/Game/Art/Animation/Humanoid/ThirdPerson/HandToHand/Locomotion/Normal/A_Humanoid_HandToHand_Idle.A_Humanoid_HandToHand_Idle";
    public string HoldAnimationFirstPerson { get; set; } =
        "/Game/Art/Animation/Humanoid/FirstPerson/HandToHand/Locomotion/Normal/A_Humanoid_HandToHand_Idle_FP.A_Humanoid_HandToHand_Idle_FP";

    // A carrier faster than this, in centimeters a second, is moving: what they carry takes its kind's moving fit
    public float MovingAbove { get; set; } = 50f;

    // A thing is put down PutDownAhead centimeters in front of the player: carried there level, CarryHeight above the
    // character's position, then let fall at most MaxDrop, stopping on the first thing it meets, the ground or another
    // carryable. With something in the way, or nothing to land on, it stays in hand. One a carrier drops by dying or
    // leaving lands FeetBelowPosition under their position, where their feet are, with no such check.
    public float PutDownAhead { get; set; } = 80f;
    public float CarryHeight { get; set; } = 40f;
    public float MaxDrop { get; set; } = 400f;
    public float FeetBelowPosition { get; set; } = 90f;

    // The shapes of things to carry: how heavy one is and how it hangs in the hand, for every kind of the category that
    // does not give its own.
    public List<CarryCategory> Categories { get; set; } =
    [
        new()
        {
            Name = "Beam", Weight = 300,
            Standing = new CarryFit { OffsetX = -8f, OffsetY = -22f, OffsetZ = 18f, Pitch = 50f, Yaw = -10f, Roll = 50f },
        },
        new() { Name = "Box", Weight = 25 },
        new() { Name = "Barrel", Weight = 30 },
        new() { Name = "Sack", Weight = 15 },
    ];

    // The kinds that can be carried, each of a category, whose weight, Speed drain and fits it takes unless it gives its
    // own. A carryable lying in the world names its kind; one whose kind left this list stays in the save file but is
    // not placed.
    public List<CarryKind> Kinds { get; set; } =
    [
        Kind("Barrel", "/Game/Forms/worldobjects/static/MiddleBarrelTopped04.MiddleBarrelTopped04", 0.6f, "Barrel"),
        Kind("Crate", "/Game/Forms/worldobjects/container/PCCrateClutterMiddle01.PCCrateClutterMiddle01", 0.45f, "Box"),
        Kind("Sack", "/Game/Forms/worldobjects/container/Sack01.Sack01", 1f, "Sack"),
        Kind("Log", "/Game/Forms/worldobjects/static/LandscapeLogBirch01.LandscapeLogBirch01", 0.35f, "Beam"),
    ];

    // Lets every player place carryables in front of them (Shift and the carry key, the next kind each time) and nudge
    // the one in hand on the numpad, logging the fit to copy here: for setting up a server, off for play.
    public bool AllowPlacement { get; set; } = true;

    // Where the carryables lying in the world are kept, next to the server DLL.
    public string SaveFile { get; set; } = "carryables.json";

    private static CarryKind Kind(string name, string asset, float scale, string category)
        => new() { Name = name, Asset = asset, Scale = scale, Category = category };
}
