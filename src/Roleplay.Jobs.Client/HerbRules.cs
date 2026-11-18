using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Client;

/// <summary>The herbs this server knows, by their plant's base form id, and how knowing them works there.</summary>
public sealed record HerbRules(
    IReadOnlyDictionary<uint, HerbRules.Herb> Herbs,
    int RecognisedBelow,
    int MasteredAbove,
    bool Markers,
    float MarkerDistance)
{
    public sealed record Herb(string Name, HerbTier Tier);

    public HerbKnowledge Know(Herb herb, HerbTier alchemy) => HerbLore.Know(herb.Tier, alchemy, RecognisedBelow, MasteredAbove);
}
