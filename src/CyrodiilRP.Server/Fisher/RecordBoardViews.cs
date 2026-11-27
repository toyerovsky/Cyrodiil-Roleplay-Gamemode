using CyrodiilRP.Server.Fishing;

namespace CyrodiilRP.Server.Fisher;

// The records board as one reader sees it. The reader's own fish stand as they are now, whatever the board last read
// says of them. Fish of one length share a rank and stand in the order they were caught.
internal static class RecordBoardViews
{
    private const int FirstRank = 1;

    // Show: the name of a holder who is not the reader, as the reader knows them.
    public static FisherRecordsView Build(RecordBoard board, RecordReader reader, Func<RecordHolder, string> show) =>
        new([.. FishCatalog.All.Select(fish => ToView(fish, board.Find(fish.Species), reader, show))]);

    private static FisherSpeciesView ToView(FishKind fish, SpeciesBoard species, RecordReader reader, Func<RecordHolder, string> show)
    {
        var own = FindOwn(fish.Species, species, reader);
        var others = species.Top
            .Where(holder => holder.CharacterId != reader.CharacterId)
            .Select(holder => new Placed(holder.CharacterId, show(holder), holder.LengthCm, holder.Grams, holder.At));
        var ordered = (own is null ? others : others.Append(own))
            .OrderByDescending(placed => placed.LengthCm).ThenBy(placed => placed.At).ThenBy(placed => placed.CharacterId)
            .ToList();
        var top = ordered.Take(RecordBoardRules.TopCount).ToList();

        return new FisherSpeciesView(
            fish.Species,
            fish.ItemName,
            [.. top.Select(placed => ToView(placed, FirstRank + ordered.Count(other => other.LengthCm > placed.LengthCm), reader))],
            own is not null && !top.Contains(own) ? ToView(own, FirstRank + species.CountLonger(own.LengthCm), reader) : null);
    }

    // The reader's best of the species as their fishing holds it now, or as the board does while that is not read yet.
    private static Placed? FindOwn(FishSpecies species, SpeciesBoard board, RecordReader reader)
    {
        if (reader.Records.TryGetValue(species, out var record))
            return new Placed(reader.CharacterId, reader.Name, record.BestLengthCm, record.BestGrams, record.BestAt);

        return board.Top.FirstOrDefault(holder => holder.CharacterId == reader.CharacterId) is { } held
            ? new Placed(reader.CharacterId, reader.Name, held.LengthCm, held.Grams, held.At)
            : null;
    }

    private static FisherRecordView ToView(Placed placed, int rank, RecordReader reader) =>
        new(rank, placed.Name, placed.LengthCm, placed.Grams, placed.At.ToUnixTimeMilliseconds(), placed.CharacterId == reader.CharacterId);

    private sealed record Placed(long CharacterId, string Name, int LengthCm, int Grams, DateTimeOffset At);
}
