using CyrodiilRP.Common.Fishing;
using OblivionMP.SDK;

namespace CyrodiilRP.Client.Fishing;

// What this game draws of one angler: the rod of its grade on the hand, bent as far as the server says (none until the
// character is drawn here, nor while a catch is shown), whether the angler turns the reel, the pose since when and
// when it plays next, the cast in the air, the float, the line to it, the fish, the small fish round the float and
// the catch held up.
internal readonly record struct AnglerScene(
    RodTier Tier,
    int Bend,
    bool Reeling,
    LocalPropHandle? Rod,
    HeldRod? Held,
    float RodRetryAt,
    AnglerPose Pose,
    float PoseSince,
    bool PosePlayed,
    float PoseNextAt,
    CastScene? Cast,
    FloatScene? Float,
    LineScene? Line,
    FishScene? Fish,
    ShoalScene? Shoal,
    CatchShow? Show);
