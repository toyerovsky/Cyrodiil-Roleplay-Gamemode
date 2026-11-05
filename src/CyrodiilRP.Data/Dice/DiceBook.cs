using Microsoft.EntityFrameworkCore;

namespace CyrodiilRP.Data.Dice;

public static class DiceBook
{
    private const char LikeEscape = '\\';

    public static async Task WriteAsync(CyrodiilContext db, DiceRoll roll, CancellationToken ct)
    {
        roll.CharacterName = ColumnLengths.Cut(roll.CharacterName, ColumnLengths.NameKey);
        roll.Asked = ColumnLengths.Cut(roll.Asked, ColumnLengths.DiceAsked);
        if (roll.Reason is { } reason)
            roll.Reason = ColumnLengths.Cut(reason, ColumnLengths.DiceReason);

        if (roll.Area is { } area)
            roll.Area = ColumnLengths.Cut(area, ColumnLengths.WorldId);

        db.DiceRolls.Add(roll);
        await db.SaveChangesAsync(ct);
    }

    // The character's dice set and last rolls, newest first, as a player loads.
    public static async Task<DiceRecord> LoadAsync(CyrodiilContext db, long characterId, int recent, CancellationToken ct)
    {
        var set = await db.Characters.AsNoTracking().Where(c => c.Id == characterId).Select(c => c.DiceSet).FirstOrDefaultAsync(ct);
        var rolls = await db.DiceRolls
            .AsNoTracking()
            .Where(r => r.CharacterId == characterId)
            .OrderByDescending(r => r.Id)
            .Take(recent)
            .ToListAsync(ct);
        return new DiceRecord(set, rolls);
    }

    public static Task SaveSetAsync(CyrodiilContext db, long characterId, string set, CancellationToken ct) =>
        db.Characters.Where(c => c.Id == characterId).ExecuteUpdateAsync(c => c.SetProperty(x => x.DiceSet, set), ct);

    // Newest first. Character: a part of the name the roll was made under.
    public static async Task<DicePage> LoadPageAsync(CyrodiilContext db, string? character, long? beforeId, int size, CancellationToken ct)
    {
        var rolls = db.DiceRolls.AsNoTracking();
        if (beforeId is { } before)
            rolls = rolls.Where(r => r.Id < before);

        if (character is { Length: > 0 } name)
        {
            var pattern = $"%{EscapeLike(name.Trim())}%";
            rolls = rolls.Where(r => EF.Functions.ILike(r.CharacterName, pattern, LikeEscape.ToString()));
        }

        var rows = await rolls.OrderByDescending(r => r.Id).Take(size + 1).ToListAsync(ct);
        var more = rows.Count > size;
        if (more)
            rows.RemoveAt(rows.Count - 1);

        return new DicePage(rows, more);
    }

    private static string EscapeLike(string text) =>
        text.Replace(LikeEscape.ToString(), $"{LikeEscape}{LikeEscape}").Replace("%", $"{LikeEscape}%").Replace("_", $"{LikeEscape}_");
}
