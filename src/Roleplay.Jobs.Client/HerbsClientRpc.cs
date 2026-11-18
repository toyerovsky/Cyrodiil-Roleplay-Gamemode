using System.Globalization;
using Microsoft.Extensions.Logging;
using ReadyM.SDK.Attributes;
using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Client;

// The client end of HerbsRpc: the generated sends, the server's messages as events for HerbsClient.
[RpcHandlersFor(typeof(HerbsRpc))]
public sealed partial class HerbsClientRpc(ILogger logger)
{
    private const char Separator = '|';

    public event Action<HerbRules>? RulesReceived;
    public event Action<HerbTier>? AlchemyReceived;
    public event Action<string>? Refused;
    public event Action<IReadOnlyList<string>, string>? Gathered;

    partial void OnHerbRules(string plants, string names, string tiers, int recognisedBelow, int masteredAbove, bool markers,
        float markerDistance)
        => Raise(nameof(OnHerbRules), () => RulesReceived?.Invoke(
            ParseRules(plants, names, tiers, recognisedBelow, masteredAbove, markers, markerDistance)));

    partial void OnHerbAlchemy(byte tier)
        => Raise(nameof(OnHerbAlchemy), () => AlchemyReceived?.Invoke(ParseTier(tier)));

    partial void OnHerbRefused(string message)
        => Raise(nameof(OnHerbRefused), () => Refused?.Invoke(message));

    partial void OnHerbGathered(string gains, string message)
        => Raise(nameof(OnHerbGathered), () => Gathered?.Invoke(gains.Split(Separator, StringSplitOptions.RemoveEmptyEntries), message));

    internal static HerbRules ParseRules(string plants, string names, string tiers, int recognisedBelow, int masteredAbove,
        bool markers, float markerDistance)
    {
        var plantList = plants.Split(Separator);
        var nameList = names.Split(Separator);
        var tierList = tiers.Split(Separator);
        if (plantList.Length != nameList.Length || plantList.Length != tierList.Length)
            throw new FormatException($"HerbRules lists {plantList.Length} plants, {nameList.Length} names and {tierList.Length} tiers.");

        var herbs = new Dictionary<uint, HerbRules.Herb>();
        for (var i = 0; i < plantList.Length; i++)
        {
            var plant = uint.Parse(plantList[i], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var tier = ParseTier(byte.Parse(tierList[i], NumberStyles.None, CultureInfo.InvariantCulture));
            herbs[plant] = new HerbRules.Herb(nameList[i], tier);
        }

        return new HerbRules(herbs, recognisedBelow, masteredAbove, markers, markerDistance);
    }

    private static HerbTier ParseTier(byte tier) => Enum.IsDefined((HerbTier)tier)
        ? (HerbTier)tier
        : throw new FormatException($"The server sent herb tier {tier}, which is none of HerbTier.");

    private void Raise(string message, Action raise)
    {
        try
        {
            raise();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Jobs: handling the server's {Message} failed.", message);
        }
    }
}
