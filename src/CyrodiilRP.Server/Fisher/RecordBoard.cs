using CyrodiilRP.Data.Fishing;
using CyrodiilRP.Server.Fishing;
using Microsoft.Extensions.Logging;

namespace CyrodiilRP.Server.Fisher;

// The best fish of the whole server, as the database gave them at one moment. A species nobody caught has no entry.
internal sealed record RecordBoard(IReadOnlyDictionary<FishSpecies, SpeciesBoard> Species)
{
    public SpeciesBoard Find(FishSpecies species) => Species.GetValueOrDefault(species, SpeciesBoard.Empty);

    public static RecordBoard Create(FishBoard stored, ILogger logger)
    {
        var top = stored.Top.ToLookup(row => row.Species, StringComparer.Ordinal);
        var species = new Dictionary<FishSpecies, SpeciesBoard>();
        foreach (var lengths in stored.Lengths.GroupBy(count => count.Species, StringComparer.Ordinal))
        {
            if (Enum.TryParse<FishSpecies>(lengths.Key, out var parsed) && Enum.IsDefined(parsed))
                species[parsed] = new SpeciesBoard([.. top[lengths.Key].Select(ToHolder)], [.. lengths]);
            else
                logger.LogError("[CyrodiilRP] The fishing records hold a species the mode does not know: {Species}.", lengths.Key);
        }

        return new RecordBoard(species);
    }

    private static RecordHolder ToHolder(FishBoardRow row) =>
        new(row.CharacterId, $"{row.FirstName} {row.LastName}", row.NamePublic, row.BestLengthCm, row.BestGrams, row.BestAt);
}
