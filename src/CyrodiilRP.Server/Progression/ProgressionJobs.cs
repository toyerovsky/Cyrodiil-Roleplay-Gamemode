using ReadyM.SDK.Attributes;
using ReadyM.SDK.Events;
using Roleplay.Jobs.Common;

namespace CyrodiilRP.Server.Progression;

[Service]
internal sealed partial class ProgressionJobs(Progression progression, RpConfig config)
{
    [EventHandler(Stage.Observe)]
    public void Ore(in OreMinedEvent mined, EventContext context) =>
        progression.Grant(JobEvents.PlayerOf(mined.Miner), ProgressionSource.Gathering, config.Progression.GatheringXp);

    [EventHandler(Stage.Observe)]
    public void Stone(in StoneQuarriedEvent quarried, EventContext context) =>
        progression.Grant(JobEvents.PlayerOf(quarried.Quarrier), ProgressionSource.Gathering, config.Progression.GatheringXp);

    [EventHandler(Stage.Observe)]
    public void Tree(in TreeFelledEvent felled, EventContext context) =>
        progression.Grant(JobEvents.PlayerOf(felled.Feller), ProgressionSource.Gathering, config.Progression.GatheringXp);
}
