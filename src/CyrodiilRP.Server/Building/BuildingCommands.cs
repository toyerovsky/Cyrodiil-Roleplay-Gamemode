using CyrodiilRP.Data.Staff;
using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Players;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Building;

// /aplot marks a new plot in architect mode, takes the one the staff member stands on away and lists them. /abuild opens
// architect mode on the plot they stand on, or off a plot on the list of plots around to choose from or mark a new one.
[Service]
internal sealed partial class BuildingCommands(
    CommandRegistry commands,
    Proximity proximity,
    Plots plots,
    Architects architects,
    PlotRemovals removals)
{
    private const string New = "new";
    private const string Remove = "remove";
    private const string List = "list";

    private void Start()
    {
        commands.AddStaff(StaffPermissions.Plot, BuildingText.CmdPlot, RunPlot, needsCharacter: true);
        commands.AddStaff(StaffPermissions.Build, BuildingText.CmdBuild, RunBuild, needsCharacter: true);
    }

    private void RunPlot(CommandCall call)
    {
        switch (call.Words.Count == 1 ? call.Words[0] : string.Empty)
        {
            case New:
                Mark(call);
                break;

            case Remove:
                Take(call);
                break;

            case List:
                ListPlots(call);
                break;

            default:
                call.Refuse(BuildingText.CmdPlot);
                break;
        }
    }

    private void RunBuild(CommandCall call)
    {
        if (call.Words.Count != 0)
        {
            call.Refuse(BuildingText.CmdBuild);
            return;
        }

        if (architects.IsOpen(call.Sender))
        {
            call.Refuse(BuildingText.AlreadyOpen);
            return;
        }

        if (!proximity.TryLocate(call.Sender.Id, out var area, out var position))
        {
            call.Refuse(BuildingText.NotInWorld);
            return;
        }

        if (plots.FindAt(area, position) is { } plot)
            architects.OpenBuilding(call.Sender, plot);
        else
            architects.OpenChoosing(call.Sender);
    }

    private void Mark(CommandCall call)
    {
        if (architects.IsOpen(call.Sender))
        {
            call.Refuse(BuildingText.AlreadyOpen);
            return;
        }

        if (!proximity.TryLocate(call.Sender.Id, out _, out _))
        {
            call.Refuse(BuildingText.NotInWorld);
            return;
        }

        architects.OpenMarking(call.Sender);
    }

    private void Take(CommandCall call)
    {
        if (FindStoodOn(call) is not { } plot)
            return;

        removals.Remove(call.Sender, plot, (text, done) =>
        {
            if (done)
                call.Reply(text);
            else
                call.Refuse(text);
        });
    }

    private void ListPlots(CommandCall call)
    {
        if (plots.All.Count == 0)
        {
            call.Reply(BuildingText.PlotsEmpty);
            return;
        }

        foreach (var plot in plots.All.OrderBy(p => p.Id))
            call.Reply(BuildingText.PlotEntry(plot.Id, plot.Area, plot.Kind, plot.Box.Width, plot.Box.Depth, plots.CountOn(plot.Id), plot.Price,
                plot.WeeklyTax));
    }

    // Refused to the caller when there is none.
    private PlotRecord? FindStoodOn(CommandCall call)
    {
        if (!proximity.TryLocate(call.Sender.Id, out var area, out var position))
        {
            call.Refuse(BuildingText.NotInWorld);
            return null;
        }

        if (plots.FindAt(area, position) is { } plot)
            return plot;

        call.Refuse(BuildingText.NoPlotHere);
        return null;
    }
}
