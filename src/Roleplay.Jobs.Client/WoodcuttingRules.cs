using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Client;

/// <summary>What <see cref="WoodcuttingRpc.WoodcuttingRules"/> says woodcutting looks like on this server. Fall is
/// null when felled trees just vanish; ChipsPerHit 0 throws no chips off a hit.</summary>
public sealed record WoodcuttingRules(TreeFilter Trees, float HitRadius, bool LumberCampsOnly, TreeFall? Fall, float ViewDistance,
    int ChipsPerHit, IReadOnlyList<string> ChipAssets, float ChipSpeed, float ChipSeconds, float ChipScale);
