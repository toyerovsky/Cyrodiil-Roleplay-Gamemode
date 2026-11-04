using Microsoft.Extensions.Logging;
using OblivionMp.Sdk.Common.Foliage;
using ReadyM.Api.Idents;
using ReadyM.SDK.Attributes;
using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Client;

/// <summary>What <see cref="WoodcuttingRpc.ChopStarted"/> tells the client to do, and about the tree.</summary>
public readonly record struct ChopOrders(string Animation, string FirstPersonAnimation, float PlayRate, float SwingSeconds,
    int MaxSwings, int HitsNeeded, float HitRadius, string Kind, int WoodTier, int? AxeTier, float WearPerHit);

// The client end of WoodcuttingRpc: the sends are generated here, the server's messages go out as events for
// WoodcuttingClient, which this cannot depend on because it depends on this.
[RpcHandlersFor(typeof(WoodcuttingRpc))]
public sealed partial class WoodcuttingClientRpc(ILogger logger)
{
    public event Action<TreeFilter, float, float, TreeFall?>? RulesReceived;
    public event Action<ChopOrders>? Started;
    public event Action<int, int>? Progressed;
    public event Action<ChopOutcome, string>? Ended;
    public event Action<FoliageId, string, float, bool, float>? TreeFelled;
    public event Action<FoliageId>? TreeGrewBack;
    public event Action<PlayerId, string, float>? OtherSwung;

    partial void OnWoodcuttingRules(string treeMeshes, string excludedMeshes, float reach, float facingDegrees,
        bool treesFall, float fallSeconds, float fallDegrees, float lieSeconds, float sinkSeconds, float sinkDepth, float fallViewDistance)
        => Raise(nameof(OnWoodcuttingRules),
            () => RulesReceived?.Invoke(new TreeFilter(TreeFilter.Split(treeMeshes), TreeFilter.Split(excludedMeshes)), reach, facingDegrees,
                treesFall ? new TreeFall(fallSeconds, fallDegrees, lieSeconds, sinkSeconds, sinkDepth, fallViewDistance) : null));

    partial void OnChopStarted(string animation, string firstPersonAnimation, float playRate, float swingSeconds,
        int maxSwings, int hitsNeeded, float hitRadius, string kind, int woodTier, int axeTier, float wearPerHit)
        => Raise(nameof(OnChopStarted), () => Started?.Invoke(new ChopOrders(animation, firstPersonAnimation, playRate,
            swingSeconds, maxSwings, hitsNeeded, hitRadius, kind, woodTier, axeTier > 0 ? axeTier : null, wearPerHit)));

    partial void OnTreeFelled(string mesh, int x, int y, int z, string kind, float scale, bool byYou, float fallYaw)
        => Raise(nameof(OnTreeFelled), () => TreeFelled?.Invoke(new FoliageId(mesh, x, y, z), kind, scale, byYou, fallYaw));

    partial void OnTreeGrewBack(string mesh, int x, int y, int z)
        => Raise(nameof(OnTreeGrewBack), () => TreeGrewBack?.Invoke(new FoliageId(mesh, x, y, z)));

    partial void OnSwung(PlayerId sender, string animation, float playRate)
        => Raise(nameof(OnSwung), () => OtherSwung?.Invoke(sender, animation, playRate));

    partial void OnChopProgress(int hits, int hitsNeeded)
        => Raise(nameof(OnChopProgress), () => Progressed?.Invoke(hits, hitsNeeded));

    partial void OnChopEnded(byte outcome, string message)
    {
        if (!Enum.IsDefined((ChopOutcome)outcome))
        {
            logger.LogWarning("Jobs: the server ended a chop with outcome {Outcome}, which is none. Ignored.", outcome);
            return;
        }
        Raise(nameof(OnChopEnded), () => Ended?.Invoke((ChopOutcome)outcome, message));
    }

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
