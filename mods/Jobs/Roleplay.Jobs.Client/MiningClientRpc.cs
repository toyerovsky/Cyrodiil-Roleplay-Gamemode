using System.Numerics;
using Microsoft.Extensions.Logging;
using ReadyM.SDK.Attributes;
using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Client;

/// <summary>What <see cref="MiningRpc.MiningRules"/> says mining looks like on this server.</summary>
public sealed record MiningRules(float HitRadius, float WearPerHit, TreeFilter QuarryRock, bool QuarryLandscape, float ViewDistance, int ChipsPerHit,
    IReadOnlyList<string> ChipAssets, float ChipSpeed, float ChipSeconds, float ChipScale, int BurstPieces, float BurstSpeed,
    float BurstSeconds, float BurstScale, bool PlacementAllowed, IReadOnlyList<string> KindNames);

/// <summary>An ore node standing, as <see cref="MiningRpc.NodeAvailable"/> tells it.</summary>
public readonly record struct OreNodeView(string Id, string Kind, string Asset, Vector3 Position, float Yaw, float Scale);

// The client end of MiningRpc: the generated sends, the server's messages as events for MiningClient.
[RpcHandlersFor(typeof(MiningRpc))]
public sealed partial class MiningClientRpc(ILogger logger)
{
    public event Action<MiningRules>? RulesReceived;
    public event Action<MarkedArea>? QuarryAvailable;
    public event Action<OreNodeView>? NodeAvailable;
    public event Action<string>? NodeRemoved;
    public event Action<string, int, int>? Progressed;
    public event Action<MiningOutcome, string>? Ended;
    public event Action<Vector3, Vector2, float>? Struck;

    partial void OnMiningRules(float hitRadius, float wearPerHit, string quarryMeshes, string excludedQuarryMeshes, bool quarryLandscape,
        float viewDistance, int chipsPerHit, string chipAssets, float chipSpeed, float chipSeconds, float chipScale,
        int burstPieces, float burstSpeed, float burstSeconds, float burstScale, bool placementAllowed, string kindNames)
        => Raise(nameof(OnMiningRules), () => RulesReceived?.Invoke(new MiningRules(hitRadius, wearPerHit,
            new TreeFilter(TreeFilter.Split(quarryMeshes), TreeFilter.Split(excludedQuarryMeshes)), quarryLandscape, viewDistance,
            chipsPerHit, Split(chipAssets), chipSpeed, chipSeconds, chipScale, burstPieces, burstSpeed, burstSeconds, burstScale,
            placementAllowed, Split(kindNames))));

    partial void OnQuarryAvailable(string name, float x, float y, float z, float radius)
        => Raise(nameof(OnQuarryAvailable), () => QuarryAvailable?.Invoke(new MarkedArea(name, new Vector3(x, y, z), radius)));

    partial void OnNodeAvailable(string id, string kind, string asset, float x, float y, float z, float yaw, float scale)
        => Raise(nameof(OnNodeAvailable), () => NodeAvailable?.Invoke(new OreNodeView(id, kind, asset, new Vector3(x, y, z), yaw, scale)));

    partial void OnNodeRemoved(string id)
        => Raise(nameof(OnNodeRemoved), () => NodeRemoved?.Invoke(id));

    partial void OnMiningProgress(string what, int hits, int hitsNeeded)
        => Raise(nameof(OnMiningProgress), () => Progressed?.Invoke(what, hits, hitsNeeded));

    partial void OnMiningEnded(byte outcome, string message)
    {
        if (!Enum.IsDefined((MiningOutcome)outcome))
        {
            logger.LogWarning("Jobs: the server ended mining with outcome {Outcome}, which is none. Ignored.", outcome);
            return;
        }
        Raise(nameof(OnMiningEnded), () => Ended?.Invoke((MiningOutcome)outcome, message));
    }

    partial void OnStruck(float x, float y, float z, float fromX, float fromY, float groundZ)
        => Raise(nameof(OnStruck), () => Struck?.Invoke(new Vector3(x, y, z), new Vector2(fromX, fromY), groundZ));

    private static string[] Split(string joined) => joined.Split('|', StringSplitOptions.RemoveEmptyEntries);

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
