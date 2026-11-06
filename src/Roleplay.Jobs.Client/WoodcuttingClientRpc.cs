using System.Numerics;
using Microsoft.Extensions.Logging;
using OblivionMP.SDK.Common.Foliage;
using ReadyM.SDK.Attributes;
using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Client;

// The client end of WoodcuttingRpc: the sends are generated here, the server's messages go out as events for
// WoodcuttingClient and TreeFallService, which this cannot depend on because they depend on this.
[RpcHandlersFor(typeof(WoodcuttingRpc))]
public sealed partial class WoodcuttingClientRpc(ILogger logger)
{
    public event Action<WoodcuttingRules>? RulesReceived;
    public event Action<MarkedArea>? LumberCampAvailable;
    public event Action<ChopProgressNews>? Progressed;
    public event Action<ChopOutcome, string, IReadOnlyList<string>>? Ended;
    public event Action<FoliageId>? TreeGrewBack;
    public event Action<Vector3, Vector2, float>? TreeStruck;

    partial void OnWoodcuttingRules(string treeMeshes, string excludedMeshes, float hitRadius, bool lumberCampsOnly,
        bool treesFall, float fallSeconds, float fallDegrees, float lieSeconds, float sinkSeconds, float sinkDepth, float fallViewDistance,
        int chipsPerHit, string chipAssets, float chipSpeed, float chipSeconds, float chipScale)
        => Raise(nameof(OnWoodcuttingRules), () => RulesReceived?.Invoke(new WoodcuttingRules(
            new TreeFilter(TreeFilter.Split(treeMeshes), TreeFilter.Split(excludedMeshes)), hitRadius, lumberCampsOnly,
            treesFall ? new TreeFall(fallSeconds, fallDegrees, lieSeconds, sinkSeconds, sinkDepth, fallViewDistance) : null,
            fallViewDistance, chipsPerHit, chipAssets.Split('|', StringSplitOptions.RemoveEmptyEntries), chipSpeed, chipSeconds, chipScale)));

    partial void OnTreeStruck(float x, float y, float z, float fromX, float fromY, float groundZ)
        => Raise(nameof(OnTreeStruck), () => TreeStruck?.Invoke(new Vector3(x, y, z), new Vector2(fromX, fromY), groundZ));

    partial void OnLumberCampAvailable(string name, float x, float y, float z, float radius)
        => Raise(nameof(OnLumberCampAvailable), () => LumberCampAvailable?.Invoke(new MarkedArea(name, new Vector3(x, y, z), radius)));

    partial void OnChopProgress(string kind, int damage, int health, float wear)
        => Raise(nameof(OnChopProgress), () => Progressed?.Invoke(new ChopProgressNews(kind, damage, health, wear)));

    partial void OnChopEnded(byte outcome, string message, string gains)
    {
        if (!Enum.IsDefined((ChopOutcome)outcome))
        {
            logger.LogWarning("Jobs: the server ended a chop with outcome {Outcome}, which is none. Ignored.", outcome);
            return;
        }
        Raise(nameof(OnChopEnded), () => Ended?.Invoke((ChopOutcome)outcome, message, gains.Split('|', StringSplitOptions.RemoveEmptyEntries)));
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
