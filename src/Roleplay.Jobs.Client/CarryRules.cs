using OblivionMpCSharpMod.Input;
using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Client;

/// <summary>What <see cref="CarryRpc.CarryRules"/> says carrying looks like on this server. An empty hold animation
/// holds nothing.</summary>
public sealed record CarryRules(Key Key, bool PlacementAllowed, string HoldAnimation, string HoldAnimationFirstPerson, float MovingAbove,
    float PutDownAhead, float CarryHeight, float MaxDrop);
