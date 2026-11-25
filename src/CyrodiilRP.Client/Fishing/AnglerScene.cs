using CyrodiilRP.Common.Fishing;
using OblivionMP.SDK;

namespace CyrodiilRP.Client.Fishing;

// What this game draws of one angler: the rod of its grade on the hand (none until the character is drawn here, nor
// while a catch is shown), the pose since when and when it plays next, the cast in the air, the float, the fish, the
// small fish round the float and the catch held up.
internal readonly record struct AnglerScene(
    RodTier Tier,
    LocalPropHandle? Rod,
    float RodRetryAt,
    AnglerPose Pose,
    float PoseSince,
    bool PosePlayed,
    float PoseNextAt,
    CastScene? Cast,
    FloatScene? Float,
    FishScene? Fish,
    ShoalScene? Shoal,
    CatchShow? Show);
