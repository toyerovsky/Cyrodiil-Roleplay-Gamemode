namespace Roleplay.Jobs.Common;

/// <summary>A pallet made of boards: what lies on it rests Height centimeters above the floor.</summary>
public sealed record Pallet(IReadOnlyList<PalletPiece> Pieces, float Height);
