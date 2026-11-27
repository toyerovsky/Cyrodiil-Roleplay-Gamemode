using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Players;
using Microsoft.Extensions.Logging;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Building;

// A plot taken away with every piece on it and the game's objects among them, from /aplot remove or architect mode.
[Service]
internal sealed partial class PlotRemovals(Plots plots, BuildingReferences references, Audit audit, Fire fire, ILogger logger)
{
    private const string RemoveAction = "plot.remove";

    // Answer: what the staff member is told, and whether it was done.
    public void Remove(OnlinePlayer by, PlotRecord plot, Action<Phrase, bool> answer)
    {
        var pieces = plots.CountOn(plot.Id);
        var objects = plots.ListOn(plot.Id).Where(p => p.RefId != 0).Select(p => p.RefId).ToList();
        fire.Run(RemoveAction, async () =>
        {
            bool removed;
            try
            {
                removed = await plots.RemovePlotAsync(plot);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "[CyrodiilRP] {Staff} could not take plot #{Plot} away.", by.Ref, plot.Id);
                if (by.IsLive)
                    answer(BuildingText.PlotRemoveFailed(plot.Id), false);

                return;
            }

            if (removed)
                audit.Log(RemoveAction, by.Ref, null, new PlotDetails(plot.Id, plot.Area, pieces));

            foreach (var refId in objects.Where(refId => !references.Delete(refId)))
                logger.LogWarning("[CyrodiilRP] Plot #{Plot} is gone, but its game object {RefId:X} was not in the world to take away.", plot.Id, refId);

            if (by.IsLive)
                answer(BuildingText.PlotRemoved(plot.Id, pieces), true);
        });
    }

    private sealed record PlotDetails(long Plot, string Area, int Pieces);
}
