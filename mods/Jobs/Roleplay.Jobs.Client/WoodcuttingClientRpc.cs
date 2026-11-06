using System.Numerics;
using Microsoft.Extensions.Logging;
using OblivionMP.SDK.Common.Foliage;
using ReadyM.SDK.Attributes;
using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Client;

/// <summary>What <see cref="WoodcuttingRpc.WoodcuttingRules"/> says woodcutting looks like on this server. Fall is
/// null when felled trees just vanish.</summary>
public sealed record WoodcuttingRules(TreeFilter Trees, float HitRadius, bool LumberCampsOnly, float WearPerHit, TreeFall? Fall);

// The client end of WoodcuttingRpc: the sends are generated here, the server's messages go out as events for
// WoodcuttingClient and TreeFallService, which this cannot depend on because they depend on this.
[RpcHandlersFor(typeof(WoodcuttingRpc))]
public sealed partial class WoodcuttingClientRpc(ILogger logger)
{
    public event Action<WoodcuttingRules>? RulesReceived;
    public event Action<MarkedArea>? LumberCampAvailable;
    public event Action<string, int, int>? Progressed;
    public event Action<ChopOutcome, string>? Ended;
    public event Action<FoliageId>? TreeGrewBack;

    partial void OnWoodcuttingRules(string treeMeshes, string excludedMeshes, float hitRadius, bool lumberCampsOnly, float wearPerHit,
        bool treesFall, float fallSeconds, float fallDegrees, float lieSeconds, float sinkSeconds, float sinkDepth, float fallViewDistance)
        => Raise(nameof(OnWoodcuttingRules), () => RulesReceived?.Invoke(new WoodcuttingRules(
            new TreeFilter(TreeFilter.Split(treeMeshes), TreeFilter.Split(excludedMeshes)), hitRadius, lumberCampsOnly, wearPerHit,
            treesFall ? new TreeFall(fallSeconds, fallDegrees, lieSeconds, sinkSeconds, sinkDepth, fallViewDistance) : null)));

    partial void OnLumberCampAvailable(string name, float x, float y, float z, float radius)
        => Raise(nameof(OnLumberCampAvailable), () => LumberCampAvailable?.Invoke(new MarkedArea(name, new Vector3(x, y, z), radius)));

    partial void OnChopProgress(string kind, int hits, int hitsNeeded)
        => Raise(nameof(OnChopProgress), () => Progressed?.Invoke(kind, hits, hitsNeeded));

    partial void OnChopEnded(byte outcome, string message)
    {
        if (!Enum.IsDefined((ChopOutcome)outcome))
        {
            logger.LogWarning("Jobs: the server ended a chop with outcome {Outcome}, which is none. Ignored.", outcome);
            return;
        }
        Raise(nameof(OnChopEnded), () => Ended?.Invoke((ChopOutcome)outcome, message));
    }

    partial void OnTreeGrewBack(string mesh, int x, int y, int z)
        => Raise(nameof(OnTreeGrewBack), () => TreeGrewBack?.Invoke(new FoliageId(mesh, x, y, z)));

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
