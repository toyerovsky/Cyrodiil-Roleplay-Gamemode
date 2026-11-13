using System.Numerics;

namespace Roleplay.Jobs.Common;

/// <summary>
/// A pallet built from one board mesh: runners along the deposit under a deck of boards across it, spaced apart, each
/// board scaled to the pallet's length or width. The game has no pallet, so it is made of what it has. Pure, so the
/// server (for how high the pile stands) and every client (to draw it) build the same one.
/// </summary>
public static class PalletLayout
{
    private const int MostRunners = 3;
    // Room between deck boards, as a share of a board's width
    private const float BoardGapShare = 0.5f;

    /// <param name="footprint">The pallet's length along the deposit (X) and width across it (Y), in centimeters.</param>
    /// <param name="boardMin">The board mesh's box at scale 1 around its origin, the low corner.</param>
    /// <param name="boardMax">The high corner.</param>
    /// <param name="woodShare">How much of the box's height, from its top, is wood: the rest is nails sticking out
    /// below, which go into the ground or the board underneath.</param>
    /// <param name="runnerInset">How far in from each side the outer runners' middles are, as a share of the pallet's
    /// width: under the deck boards' nails.</param>
    /// <returns>Null when the footprint or the board has no size.</returns>
    public static Pallet? Of(Vector2 footprint, Vector3 boardMin, Vector3 boardMax, float woodShare, float runnerInset)
    {
        var size = boardMax - boardMin;
        if (footprint.X <= 0f || footprint.Y <= 0f || size.X <= 0f || size.Y <= 0f || size.Z <= 0f)
            return null;

        var wood = new BoardWood(boardMax.Z - size.Z * Math.Clamp(woodShare, 0.01f, 1f), boardMax.Z);
        // A board's long side is turned to lie along the deposit's X
        var turn = size.X >= size.Y ? 0f : 90f;
        var length = MathF.Max(size.X, size.Y);
        var width = MathF.Min(size.X, size.Y);
        var pieces = new List<PalletPiece>();

        var runnerScale = footprint.X / length;
        var runnerWidth = width * runnerScale;
        var runnerHeight = wood.Thickness * runnerScale;
        var runners = Math.Clamp((int)(footprint.Y / runnerWidth), 1, MostRunners);
        var outer = MathF.Min(footprint.Y * (0.5f - Math.Clamp(runnerInset, 0f, 0.5f)), (footprint.Y - runnerWidth) / 2f);
        foreach (var across in Spread(outer, runners))
            pieces.Add(Piece(boardMin, boardMax, wood, new Vector2(0f, across), 0f, turn, runnerScale));

        var deckScale = footprint.Y / length;
        var deckWidth = width * deckScale;
        var gap = deckWidth * BoardGapShare;
        var boards = Math.Max(1, (int)MathF.Floor((footprint.X + gap) / (deckWidth + gap)));
        foreach (var along in Spread((footprint.X - deckWidth) / 2f, boards))
            pieces.Add(Piece(boardMin, boardMax, wood, new Vector2(along, 0f), runnerHeight, turn + 90f, deckScale));

        return new Pallet(pieces, runnerHeight + wood.Thickness * deckScale);
    }

    // Where the wood of a board is in its box at scale 1, bottom to top
    private sealed record BoardWood(float Bottom, float Top)
    {
        public float Thickness => Top - Bottom;
    }

    // count middles spread evenly from -outer to outer
    private static IEnumerable<float> Spread(float outer, int count)
    {
        if (count == 1)
        {
            yield return 0f;
            yield break;
        }
        var step = 2f * outer / (count - 1);
        for (var index = 0; index < count; index++)
            yield return -outer + index * step;
    }

    // The board's origin, so that its box is centred on middle with the bottom of its wood at bottom
    private static PalletPiece Piece(Vector3 boardMin, Vector3 boardMax, BoardWood wood, Vector2 middle, float bottom, float yaw, float scale)
    {
        var centre = (boardMin + boardMax) / 2f * scale;
        var radians = yaw * MathF.PI / 180f;
        var cos = MathF.Cos(radians);
        var sin = MathF.Sin(radians);
        var turned = new Vector2(centre.X * cos - centre.Y * sin, centre.X * sin + centre.Y * cos);
        return new PalletPiece(new Vector3(middle.X - turned.X, middle.Y - turned.Y, bottom - wood.Bottom * scale), yaw, scale);
    }
}
