using CyrodiilRP.Data.Fishing;

namespace CyrodiilRP.Server.Fisher;

// A species on the records board. Top: its longest fish, the best first. Lengths: every best length of the species with
// how many characters hold it.
internal sealed record SpeciesBoard(IReadOnlyList<RecordHolder> Top, IReadOnlyList<FishLengthCount> Lengths)
{
    public static readonly SpeciesBoard Empty = new([], []);

    public int CountLonger(int lengthCm) => Lengths.Where(each => each.LengthCm > lengthCm).Sum(each => each.Characters);
}
