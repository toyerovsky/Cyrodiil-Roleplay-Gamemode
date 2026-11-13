using System.Numerics;
using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Client;

/// <summary>What <see cref="DocksRpc.DocksRules"/> says the docks look like on this server.</summary>
/// <param name="PalletBoard">The asset the pallets are built of; empty for none.</param>
/// <param name="PalletFootprint">The pallet's length along a deposit (X) and width across it (Y).</param>
/// <param name="PalletWoodShare">How much of the board's height, from the top, is wood (<see cref="PalletLayout"/>).</param>
/// <param name="PalletRunnerInset">How far in the outer runners lie, as a share of the pallet's width.</param>
/// <param name="CrateBottom">The crate's bottom from its origin: the ground is this far from a deposit's floor.</param>
public sealed record DocksRules(bool SetupAllowed, float ZoneSize, string CrateAsset, float CrateScale, IReadOnlyList<string> RoleNames,
    string PalletBoard, Vector2 PalletFootprint, float PalletWoodShare, float PalletRunnerInset, float CrateBottom);
