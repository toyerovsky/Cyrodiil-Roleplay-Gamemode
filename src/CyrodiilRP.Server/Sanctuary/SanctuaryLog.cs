using CyrodiilRP.Data;
using CyrodiilRP.Data.Sanctuary;
using CyrodiilRP.Server.Admin;
using CyrodiilRP.Server.Infrastructure;
using Microsoft.Extensions.Logging;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Sanctuary;

// Every incident in the database, for the staff, and a line in the server's log. Writes go through the one writer.
[Service]
internal sealed partial class SanctuaryLog(Persistence persistence, Fire fire, ILogger logger)
{
    // As many incidents as the panel's Sanctuary tab shows at a time.
    public const int PageSize = 50;

    private const string WriteJob = "sanctuary.write";

    public void Write(OpenIncident incident, string areaName)
    {
        var row = new SanctuaryIncident
        {
            At = incident.At,
            Area = incident.Area,
            AreaName = areaName,
            VictimKind = incident.Kind,
            VictimAccount = incident.Player?.Account,
            VictimName = incident.VictimName,
            Damage = MathF.Round(incident.Damage, 1),
            Died = incident.Died,
            Verdict = incident.Verdict,
            Aggressors = incident.Suspects.Select(suspect => suspect.Name).ToArray(),
            AggressorAccounts = incident.Suspects.Select(suspect => suspect.Account).ToArray(),
        };

        logger.LogInformation("[CyrodiilRP] Sanctuary: {Victim} lost {Damage:0.#} health in {Area}{Died}, verdict {Verdict}, aggressors: {Aggressors}.",
            row.VictimName, row.Damage, areaName, row.Died ? " and died" : "", row.Verdict,
            row.Aggressors.Length == 0 ? "none" : string.Join(", ", row.Aggressors));
        fire.Run(WriteJob, () => persistence.Writer.EnqueueAsync(WriteJob, (db, ct) => SanctuaryBook.WriteAsync(db, row, ct)));
    }

    public async Task<AdminIncidentsView> ReadPageAsync(AdminIncidentFilter filter)
    {
        var page = await persistence.Reader.RunAsync((db, ct) => SanctuaryBook.LoadPageAsync(db, filter.Name, filter.Before, PageSize, ct));
        var incidents = page.Incidents
            .Select(row => new AdminIncidentView(row.Id, row.At.ToUnixTimeMilliseconds(), row.Area, row.AreaName, row.VictimName, row.VictimKind,
                row.Damage, row.Died, row.Verdict, row.Aggressors))
            .ToList();
        return new AdminIncidentsView(incidents, page.More, filter.Before is not null);
    }
}
