using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Client;

/// <summary>What <see cref="MiningRpc.MiningRules"/> says mining looks like on this server.</summary>
public sealed record MiningRules(float HitRadius, TreeFilter QuarryRock, bool QuarryLandscape, float ViewDistance, int ChipsPerHit,
    IReadOnlyList<string> ChipAssets, float ChipSpeed, float ChipSeconds, float ChipScale, int BurstPieces, float BurstSpeed,
    float BurstSeconds, float BurstScale, bool PlacementAllowed, IReadOnlyList<string> KindNames, IReadOnlyList<string> KindAssets,
    IReadOnlyList<float> KindScales);
