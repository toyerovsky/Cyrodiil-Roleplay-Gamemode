using CyrodiilRP.Data.Characters;
using Microsoft.EntityFrameworkCore;

namespace CyrodiilRP.Data;

public static class AuditBook
{
    // As many accounts as a filter by name looks at: a filter that matches more is too loose to read anyway.
    private const int MostMatchedAccounts = 50;
    private const char LikeEscape = '\\';

    public static async Task WriteAsync(CyrodiilContext db, IReadOnlyCollection<AuditEntry> entries, CancellationToken ct)
    {
        foreach (var entry in entries)
        {
            if (entry.Details is { } details)
                entry.Details = ColumnLengths.Cut(details, ColumnLengths.AuditDetails);
        }

        db.AuditEntries.AddRange(entries);
        await db.SaveChangesAsync(ct);
    }

    public static async Task<AuditPage> LoadPageAsync(CyrodiilContext db, AuditQuery query, CancellationToken ct)
    {
        var entries = db.AuditEntries.AsNoTracking();
        if (query.BeforeId is { } before)
            entries = entries.Where(e => e.Id < before);

        if (query.Action is { Length: > 0 } action)
            entries = entries.Where(e => e.Action.StartsWith(action));

        if (query.From is { } from)
            entries = entries.Where(e => e.At >= from);

        if (query.To is { } to)
            entries = entries.Where(e => e.At < to);

        if (query.Actor is { Length: > 0 } actor)
        {
            var refs = await FindRefsAsync(db, actor, ct);
            entries = entries.Where(e => e.ActorRef != null && refs.Contains(e.ActorRef));
        }

        if (query.Target is { Length: > 0 } target)
        {
            var refs = await FindRefsAsync(db, target, ct);
            entries = entries.Where(e => e.TargetRef != null && refs.Contains(e.TargetRef));
        }

        var rows = await entries.OrderByDescending(e => e.Id).Take(query.Size + 1).ToListAsync(ct);
        var more = rows.Count > query.Size;
        if (more)
            rows.RemoveAt(rows.Count - 1);

        var names = await LoadNamesAsync(db, rows.SelectMany(e => new[] { e.ActorRef, e.TargetRef }), ct);
        string? FindName(string? reference) => reference is null ? null : names.GetValueOrDefault(reference, reference);
        return new AuditPage(rows.Select(e => new AuditRow(e.Id, e.At, e.Action, FindName(e.ActorRef), FindName(e.TargetRef), e.Details)).ToList(), more);
    }

    // Of the accounts whose nickname or character's name holds the text, or whose ReadyM id it is, and of their characters.
    private static async Task<List<string>> FindRefsAsync(CyrodiilContext db, string text, CancellationToken ct)
    {
        var pattern = $"%{EscapeLike(text.Trim())}%";
        var exact = Guid.TryParse(text.Trim(), out var readyMId) ? readyMId : (Guid?)null;
        var accounts = await db.Accounts
            .AsNoTracking()
            .Where(a => a.ReadyMId == exact
                        || EF.Functions.ILike(a.LastNickname, pattern, LikeEscape.ToString())
                        || a.Characters.AsQueryable().Select(NamedCharacter.From).Any(c => EF.Functions.ILike(c.Name, pattern, LikeEscape.ToString())))
            .OrderBy(a => a.Id)
            .Take(MostMatchedAccounts)
            .Select(a => new { a.ReadyMId, Characters = a.Characters.Select(c => c.Id).ToList() })
            .ToListAsync(ct);

        return accounts
            .SelectMany(a => a.Characters.Select(AuditRefs.FormatCharacter).Prepend(AuditRefs.FormatAccount(a.ReadyMId)))
            .ToList();
    }

    // Each reference to an account or a character as the staff read it: the character's name and the nickname.
    public static async Task<Dictionary<string, string>> LoadNamesAsync(CyrodiilContext db, IEnumerable<string?> references, CancellationToken ct)
    {
        var accountIds = new HashSet<Guid>();
        var characterIds = new HashSet<long>();
        foreach (var reference in references)
        {
            if (AuditRefs.TryAccount(reference, out var readyMId))
                accountIds.Add(readyMId);
            else if (AuditRefs.TryCharacter(reference, out var id))
                characterIds.Add(id);
        }

        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        if (accountIds.Count > 0)
        {
            var accounts = await db.Accounts
                .AsNoTracking()
                .Where(a => accountIds.Contains(a.ReadyMId))
                .Select(a => new
                {
                    a.ReadyMId,
                    a.LastNickname,
                    Character = a.Characters.AsQueryable().Select(NamedCharacter.From).OrderBy(c => c.Id).Select(c => c.Name).FirstOrDefault(),
                })
                .ToListAsync(ct);
            foreach (var account in accounts)
                names[AuditRefs.FormatAccount(account.ReadyMId)] = FormatName(account.Character, account.LastNickname);
        }

        if (characterIds.Count > 0)
        {
            var characters = await db.Characters
                .AsNoTracking()
                .Where(c => characterIds.Contains(c.Id))
                .Select(NamedCharacter.From)
                .Join(db.Accounts, c => c.AccountId, a => a.Id, (c, a) => new { c.Id, c.Name, a.LastNickname })
                .ToListAsync(ct);
            foreach (var character in characters)
                names[AuditRefs.FormatCharacter(character.Id)] = FormatName(character.Name, character.LastNickname);
        }

        return names;
    }

    private static string FormatName(string? character, string nickname)
    {
        if (character is null)
            return nickname;

        return nickname.Length == 0 ? character : $"{character} ({nickname})";
    }

    private static string EscapeLike(string text) =>
        text.Replace(LikeEscape.ToString(), $"{LikeEscape}{LikeEscape}").Replace("%", $"{LikeEscape}%").Replace("_", $"{LikeEscape}_");
}
