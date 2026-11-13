using System.Numerics;
using Microsoft.Extensions.Logging;
using ReadyM.SDK.Attributes;
using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Client;

// The client end of DocksRpc: the generated sends, the server's messages as events for DocksClient and ForemanLabels.
[RpcHandlersFor(typeof(DocksRpc))]
public sealed partial class DocksClientRpc(ILogger logger)
{
    public event Action<DocksRules>? RulesReceived;
    public event Action<int, string>? Labelled;
    public event Action<DockZoneView>? ZoneAvailable;
    public event Action<string>? ZoneRemoved;
    public event Action<string, bool>? Noted;

    partial void OnDocksRules(bool setupAllowed, float zoneSize, string crateAsset, float crateScale, string roleNames,
        string palletBoard, float palletLength, float palletWidth, float palletWoodShare, float palletRunnerInset, float crateBottom)
        => Raise(nameof(OnDocksRules), () => RulesReceived?.Invoke(new DocksRules(setupAllowed, zoneSize, crateAsset, crateScale,
            roleNames.Split('|', StringSplitOptions.RemoveEmptyEntries), palletBoard, new Vector2(palletLength, palletWidth),
            palletWoodShare, palletRunnerInset, crateBottom)));

    partial void OnForemanLabel(int origin, string name)
        => Raise(nameof(OnForemanLabel), () => Labelled?.Invoke(origin, name));

    partial void OnZoneAvailable(string id, byte kind, float x, float y, float z, float yaw)
    {
        if (!Enum.IsDefined((DockZoneKind)kind))
        {
            logger.LogWarning("Jobs: the server sent deposit {Id} of kind {Kind}, which is none. Ignored.", id, kind);
            return;
        }
        Raise(nameof(OnZoneAvailable), () => ZoneAvailable?.Invoke(new DockZoneView(id, (DockZoneKind)kind, new Vector3(x, y, z), yaw)));
    }

    partial void OnZoneRemoved(string id)
        => Raise(nameof(OnZoneRemoved), () => ZoneRemoved?.Invoke(id));

    partial void OnDocksNote(string message, bool refusal)
        => Raise(nameof(OnDocksNote), () => Noted?.Invoke(message, refusal));

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
