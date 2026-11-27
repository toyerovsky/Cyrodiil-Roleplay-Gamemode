using System.Numerics;
using CyrodiilRP.Common.Fishing;

namespace CyrodiilRP.Client.Fishing;

// How an angler is drawn: the game's meshes, animations, effects and sounds, where they hang, how big and for how long.
// Centimetres, degrees (pitch, yaw, roll) and seconds.
internal static class AnglerLooks
{
    // The right hand, where the game hangs a held prop.
    public const string HandSocket = "Weapon_Socket";

    // The mode's own rods, from the mod's container (mod/paks, made in design/models/fishing-rods), which the launcher
    // copies into the game's Content\Paks: held at the grip, the tip where a staff's head is.
    public static string RodOf(RodTier tier) => tier switch
    {
        RodTier.Basic => "/Game/CyrodiilRP/Fishing/SM_FishingRod_Basic.SM_FishingRod_Basic",
        RodTier.Sturdy => "/Game/CyrodiilRP/Fishing/SM_FishingRod_Sturdy.SM_FishingRod_Sturdy",
        RodTier.Master => "/Game/CyrodiilRP/Fishing/SM_FishingRod_Master.SM_FishingRod_Master",
    };

    // The same rod with its blank bent toward its guides, 1 a little to FishingGear.MostBend the most.
    public static string BentRodOf(RodTier tier, int bend)
    {
        var name = $"SM_FishingRod_{tier}_Bend{bend}";
        return $"/Game/CyrodiilRP/Fishing/{name}.{name}";
    }

    // The centre of each rod's tip ring on its mesh, where the line leaves it, as export_rods.py writes them to
    // rods.json: straight, then each bend.
    public static Vector3 RodTipOf(RodTier tier, int bend) => tier switch
    {
        RodTier.Basic => bend switch
        {
            0 => new Vector3(0.28f, -160.4f, -3.63f),
            1 => new Vector3(0.28f, -158.99f, -16.96f),
            2 => new Vector3(0.28f, -155.28f, -32.53f),
            3 => new Vector3(0.28f, -148.31f, -49.68f),
            4 => new Vector3(0.28f, -137.22f, -67.34f),
            5 => new Vector3(0.28f, -121.4f, -84.01f),
            _ => throw new ArgumentOutOfRangeException(nameof(bend), bend, "A rod bends 0 to 5."),
        },
        RodTier.Sturdy => bend switch
        {
            0 => new Vector3(0f, -170.4f, -1.7f),
            1 => new Vector3(0f, -169.29f, -15.67f),
            2 => new Vector3(0f, -165.83f, -32.07f),
            3 => new Vector3(0f, -159f, -50.24f),
            4 => new Vector3(0f, -147.83f, -69.12f),
            5 => new Vector3(0f, -131.6f, -87.12f),
            _ => throw new ArgumentOutOfRangeException(nameof(bend), bend, "A rod bends 0 to 5."),
        },
        RodTier.Master => bend switch
        {
            0 => new Vector3(0f, -180.4f, -1.4f),
            1 => new Vector3(0f, -179.29f, -16.16f),
            2 => new Vector3(0f, -175.72f, -33.5f),
            3 => new Vector3(0f, -168.6f, -52.74f),
            4 => new Vector3(0f, -156.88f, -72.74f),
            5 => new Vector3(0f, -139.81f, -91.87f),
            _ => throw new ArgumentOutOfRangeException(nameof(bend), bend, "A rod bends 0 to 5."),
        },
    };

    // The line from the rod's tip to the float: a pale grey, this thick, hanging this share of its length while the
    // float flies, lies waiting, or a hooked fish leaves it slack. A taut line hangs none.
    public const byte LineRed = 176;
    public const byte LineGreen = 176;
    public const byte LineBlue = 168;
    public const float LineThickness = 0.8f;
    public const float LineSagFlying = 0.03f;
    public const float LineSagWaiting = 0.1f;
    public const float LineSagSlack = 0.08f;
    public const float LineSagGlide = 6f;
    // The hand closed on the bare rod ahead of the grip: the rod slides this far toward its tip (the mesh's -Y), so the
    // hand holds the grip and the reel sits just behind it.
    public static readonly Vector3 RodOffset = new(0f, -10f, 0f);
    // The rods are modelled with the reel and the guides under them, which the socket turns upward: half a turn about
    // their length (pitch, the mesh's Y) hangs them under the rod again. A staff needs neither.
    public static readonly Vector3 RodRotation = new(180f, 0f, 0f);
    public static readonly Vector3 StaffOffset = Vector3.Zero;
    public static readonly Vector3 StaffRotation = Vector3.Zero;
    public const float RodScale = 1f;
    public const float RodRetrySeconds = 1f;

    private const string Animations = "/Game/Art/Animation/Humanoid/ThirdPerson/";
    public const string Holding = Animations + "Staff/Locomotion/Normal/A_Humanoid_Staff_Idle.A_Humanoid_Staff_Idle";
    public const string Casting = Animations + "Staff/Combat/Attacks/A_Humanoid_Staff_Attack_Right.A_Humanoid_Staff_Attack_Right";
    public const string Striking = Animations + "Staff/Combat/Block/A_Humanoid_Staff_Block_Hit.A_Humanoid_Staff_Block_Hit";
    public const string Fighting = Animations + "Staff/Combat/Block/A_Humanoid_Staff_Block_Idle.A_Humanoid_Staff_Block_Idle";
    public const string Showing = Animations + "IdleAnims/A_Humanoid_OneHand_LookAt_Weapon.A_Humanoid_OneHand_LookAt_Weapon";

    // The mode's own, from a container of its own (design/models/fishing-anim): the fight's pose with the left hand
    // turning the reel, twice a second. Played again a whole number of turns in, so the hand stays on its circle.
    public const string Reeling = "/Game/CyrodiilRP/Fishing/A_Fishing_Reel.A_Fishing_Reel";
    public const float ReelOverlap = 0.5f;

    // A pose that loops is played again this long before it ends, or after the retry when it did not play.
    public const float PoseOverlap = 0.2f;
    public const float PoseRetrySeconds = 1f;
    public const float PoseShortest = 0.1f;
    // A swing or a jerk not played by then (the character not drawn here) is left out.
    public const float StalePoseSeconds = 1f;
    public const float PoseBlendOutSeconds = 0.3f;

    public const string FloatMesh = "/Game/Art/Environment/clutter/SM_kelpgasbladder.SM_kelpgasbladder";
    public const float FloatScale = 0.45f;
    public static readonly Vector3 FloatRotation = Vector3.Zero;
    // How high over the water line the float's origin is put.
    public const float FloatRaise = 0f;
    public const float FloatGlide = 10f;
    // A move this long is a new place: the float goes there at once.
    public const float FloatJumps = 300f;
    public const float BittenDepth = 25f;
    public const float DraggedDepth = 6f;
    public const float SinkGlide = 8f;
    // So floats side by side do not bob as one.
    public const float BobPhaseSeconds = 0.37f;

    // The float leaves the rod this far into the swing, from above the hand: the character's position is its middle.
    public const float ReleaseSeconds = 0.45f;
    public const float ReleaseAhead = 60f;
    public const float ReleaseUp = 110f;

    public const string FishMesh = "/Game/Art/Creatures/Fish/SM_Fish.SM_Fish";
    // The mesh is long along its Y: this yaw puts its head the way it swims, and a roll tilts its nose.
    public const float FishMeshYaw = -90f;
    public const float FishTiltSign = 1f;
    // A fish whose length the server did not send is drawn this long.
    public const int FishShownCm = 30;
    // A new fish is placed again for a length this share off the one drawn.
    public const float FishRescales = 0.05f;
    public const float ApproachFrom = 250f;
    public const float CircleGlide = 3f;
    public const float DartGlide = 12f;
    public const float DiveGlide = 8f;
    public const float FollowGlide = 6f;
    public const float LeaveGlide = 6f;
    public const float FishTurn = 8f;
    // A step shorter than this does not turn the fish.
    public const float FishTurnsOver = 0.3f;
    public const float DartSeconds = 0.5f;
    public const float BiteDepth = 60f;
    public const float FightDepth = 30f;
    public const float EscapeSeconds = 1.6f;
    // How quickly a small fish of the shoal keeps to its circle, and so how fast it swims in to it.
    public const float ShoalGlide = 1.2f;

    public static readonly Vector3 ShowOffset = Vector3.Zero;
    public static readonly Vector3 ShowRotation = Vector3.Zero;
    // The look at the hand plays at ShowRate, so that it runs the whole show: seen in the game, an angler with no
    // animation playing stands with the arms spread. The server ends the show sooner, at the next cast or when the
    // catch is let go.
    public const float ShowLookSeconds = 3.2f;
    public const float ShowRate = 0.5f;
    public const float ShowSeconds = ShowLookSeconds / ShowRate - PoseOverlap;

    private const string Effects = "/Game/Art/FX/";
    public const string Impact = Effects + "Weapons/NS_Impact_Water_01.NS_Impact_Water_01";
    public const string Plop = Effects + "Character/NS_CharStep_Water_01.NS_CharStep_Water_01";
    public const string Ripples = Effects + "Environment/NS_Static_WaterRipples_01.NS_Static_WaterRipples_01";
    public const string SplashSmall = Effects + "Environment/NS_Static_WaterSplash_01.NS_Static_WaterSplash_01";
    public const string SplashMedium = Effects + "Environment/NS_Static_WaterSplash_02.NS_Static_WaterSplash_02";
    public const string SplashLarge = Effects + "Environment/NS_Static_WaterSplash_03.NS_Static_WaterSplash_03";
    public const string SplashHuge = Effects + "Environment/NS_Static_WaterSplash_04.NS_Static_WaterSplash_04";
    public const string Bubbles = Effects + "Environment/NS_Static_UnderwaterBubbles.NS_Static_UnderwaterBubbles";

    // The game's splashes keep running unseen after they show, so each is taken away after this.
    public const float SplashSeconds = 4f;
    public const float LandingScale = 0.4f;
    public const float RipplesScale = 0.5f;
    public const float RipplesSeconds = 3f;
    public const float NibbleScale = 1f;
    public const float TwitchScale = 0.7f;
    public const float BiteScale = 1f;
    public const float ThrashScale = 0.8f;
    public const float JumpScale = 1f;
    public const float ReentryScale = 0.7f;
    public const float CatchScale = 1f;
    public const float SnapScale = 0.6f;
    public const float ReleaseScale = 0.6f;
    public const float BubblesScale = 0.5f;
    public const float BubblesDepth = 30f;
    public const float BubblesSeconds = 1.5f;

    private const string Sounds = "/Game/WwiseAudio/";
    public const string SplashSound = Sounds + "Character/Foley/char_fol_water_splash.char_fol_water_splash";
    public const string ThrashSound = Sounds + "Creature/Slaughterfish/cre_slaughterfish_swim.cre_slaughterfish_swim";
    public const string WhooshSound = Sounds + "Game_Object/Trap/obj_trp_bluntobject_swish.obj_trp_bluntobject_swish";
}
