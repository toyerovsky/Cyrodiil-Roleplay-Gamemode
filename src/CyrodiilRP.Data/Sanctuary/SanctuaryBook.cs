using Microsoft.EntityFrameworkCore;

namespace CyrodiilRP.Data.Sanctuary;

public static class SanctuaryBook
{
    private const char LikeEscape = '\\';

    public static async Task WriteAsync(CyrodiilContext db, SanctuaryIncident incident, CancellationToken ct)
    {
        incident.Area = ColumnLengths.Cut(incident.Area, ColumnLengths.WorldId);
        incident.AreaName = ColumnLengths.Cut(incident.AreaName, ColumnLengths.AreaName);
        incident.VictimName = ColumnLengths.Cut(incident.VictimName, ColumnLengths.Nickname);
        incident.Aggressors = incident.Aggressors.Select(name => ColumnLengths.Cut(name, ColumnLengths.Nickname)).ToArray();

        db.SanctuaryIncidents.Add(incident);
        await db.SaveChangesAsync(ct);
    }

    // Newest first. Name: a part of the victim's name or of an aggressor's.
    public static async Task<SanctuaryPage> LoadPageAsync(CyrodiilContext db, string? name, long? beforeId, int size, CancellationToken ct)
    {
        var incidents = db.SanctuaryIncidents.AsNoTracking();
        if (beforeId is { } before)
            incidents = incidents.Where(i => i.Id < before);

        if (name is { Length: > 0 } part)
        {
            var pattern = $"%{EscapeLike(part.Trim())}%";
            var escape = LikeEscape.ToString();
            incidents = incidents.Where(i => EF.Functions.ILike(i.VictimName, pattern, escape)
                                             || i.Aggressors.Any(aggressor => EF.Functions.ILike(aggressor, pattern, escape)));
        }

        var rows = await incidents.OrderByDescending(i => i.Id).Take(size + 1).ToListAsync(ct);
        var more = rows.Count > size;
        if (more)
            rows.RemoveAt(rows.Count - 1);

        return new SanctuaryPage(rows, more);
    }

    private static string EscapeLike(string text) =>
        text.Replace(LikeEscape.ToString(), $"{LikeEscape}{LikeEscape}").Replace("%", $"{LikeEscape}%").Replace("_", $"{LikeEscape}_");
}
