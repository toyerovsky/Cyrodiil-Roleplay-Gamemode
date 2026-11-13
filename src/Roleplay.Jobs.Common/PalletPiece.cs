using System.Numerics;

namespace Roleplay.Jobs.Common;

/// <summary>
/// One board of a pallet: where the board's origin goes, from the middle of the deposit's floor along the deposit's own
/// axes, turned Yaw degrees more than the deposit, at Scale.
/// </summary>
public sealed record PalletPiece(Vector3 Offset, float Yaw, float Scale);
