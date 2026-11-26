using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Zones;

[Service]
internal sealed partial class ZoneSpawnCorrections(ZoneSpawns spawns)
{
    [UpdateOrder(ZoneSpawns.AfterRespawn)]
    private void Update() => spawns.CorrectLevels();
}
