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
    public static readonly Vector3 RodOffset = Vector3.Zero;
    // The rods are modelled with the reel and the guides under them, which the socket turns upward: half a turn about
    // their length (pitch, the mesh's Y) hangs them under the rod again. A staff needs no turn.
    public static readonly Vector3 RodRotation = new(180f, 0f, 0f);
    public static readonly Vector3 StaffRotation = Vector3.Zero;
    public const float RodScale = 1f;
    public const float RodRetrySeconds = 1f;

    private const string Animations = "/Game/Art/Animation/Humanoid/ThirdPerson/";
    public const string Holding = Animations + "Staff/Locomotion/Normal/A_Humanoid_Staff_Idle.A_Humanoid_Staff_Idle";
    public const string Casting = Animations + "Staff/Combat/Attacks/A_Humanoid_Staff_Attack_Right.A_Humanoid_Staff_Attack_Right";
    public const string Striking = Animations + "Staff/Combat/Block/A_Humanoid_Staff_Block_Hit.A_Humanoid_Staff_Block_Hit";
    public const string Fighting = Animations + "Staff/Combat/Block/A_Humanoid_Staff_Block_Idle.A_Humanoid_Staff_Block_Idle";
    public const string Showing = Animations + "IdleAnims/A_Humanoid_OneHand_LookAt_Weapon.A_Humanoid_OneHand_LookAt_Weapon";

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
    public const float ShowSeconds = 3f;

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
    public const float BiteScale = 1f;
    public const float ThrashScale = 0.8f;
    public const float JumpScale = 1f;
    public const float ReentryScale = 0.7f;
    public const float CatchScale = 1f;
    public const float SnapScale = 0.6f;
    public const float BubblesScale = 0.5f;
    public const float BubblesDepth = 30f;
    public const float BubblesSeconds = 1.5f;

    private const string Sounds = "/Game/WwiseAudio/";
    public const string SplashSound = Sounds + "Character/Foley/char_fol_water_splash.char_fol_water_splash";
    public const string ThrashSound = Sounds + "Creature/Slaughterfish/cre_slaughterfish_swim.cre_slaughterfish_swim";
    public const string WhooshSound = Sounds + "Game_Object/Trap/obj_trp_bluntobject_swish.obj_trp_bluntobject_swish";
}
