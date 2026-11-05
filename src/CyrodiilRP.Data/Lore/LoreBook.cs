using Microsoft.EntityFrameworkCore;

namespace CyrodiilRP.Data.Lore;

public static class LoreBook
{
    public static async Task<LoreCardRecord> LoadCardAsync(CyrodiilContext db, long characterId, CancellationToken ct)
    {
        var namePublic = await db.Characters.AsNoTracking().Where(c => c.Id == characterId).Select(c => c.NamePublic).FirstOrDefaultAsync(ct);
        var fields = await db.LoreFields.AsNoTracking().Where(f => f.CharacterId == characterId).ToListAsync(ct);
        return new LoreCardRecord(namePublic, fields);
    }

    // Each row replaces the one of its field, or is added.
    public static async Task SaveFieldsAsync(CyrodiilContext db, long characterId, IReadOnlyList<LoreField> rows, CancellationToken ct)
    {
        var ids = rows.Select(row => row.FieldId).ToList();
        var stored = await db.LoreFields.Where(f => f.CharacterId == characterId && ids.Contains(f.FieldId)).ToDictionaryAsync(f => f.FieldId, ct);
        foreach (var row in rows)
        {
            var text = row.Text is { } written ? ColumnLengths.Cut(written, ColumnLengths.LoreText) : null;
            if (!stored.TryGetValue(row.FieldId, out var field))
            {
                field = new LoreField { CharacterId = characterId, FieldId = row.FieldId };
                db.LoreFields.Add(field);
                stored[row.FieldId] = field;
            }

            field.CatalogId = row.CatalogId;
            field.Text = text;
            field.Number = row.Number;
            field.Fixed = row.Fixed;
            field.Rolls = row.Rolls;
            field.Visibility = row.Visibility;
            field.UpdatedAt = row.UpdatedAt;
        }

        await db.SaveChangesAsync(ct);
    }

    public static Task SetNamePublicAsync(CyrodiilContext db, long characterId, bool namePublic, CancellationToken ct) =>
        db.Characters.Where(c => c.Id == characterId).ExecuteUpdateAsync(c => c.SetProperty(x => x.NamePublic, namePublic), ct);

    // Both directions: whom the character knows, and who knows it.
    public static Task<List<LoreIntroduction>> LoadIntroductionsAsync(CyrodiilContext db, long characterId, CancellationToken ct) =>
        db.LoreIntroductions.AsNoTracking()
            .Where(i => i.KnowerCharacterId == characterId || i.KnownCharacterId == characterId)
            .ToListAsync(ct);

    // Both now know each other. A direction already known keeps its first date.
    public static async Task IntroduceAsync(CyrodiilContext db, long first, long second, DateTimeOffset at, CancellationToken ct)
    {
        var known = await db.LoreIntroductions
            .Where(i => (i.KnowerCharacterId == first && i.KnownCharacterId == second) || (i.KnowerCharacterId == second && i.KnownCharacterId == first))
            .Select(i => i.KnowerCharacterId)
            .ToListAsync(ct);
        if (!known.Contains(first))
            db.LoreIntroductions.Add(new LoreIntroduction { KnowerCharacterId = first, KnownCharacterId = second, At = at });

        if (!known.Contains(second))
            db.LoreIntroductions.Add(new LoreIntroduction { KnowerCharacterId = second, KnownCharacterId = first, At = at });

        await db.SaveChangesAsync(ct);
    }

    // The knower no longer knows the known. False when it did not.
    public static async Task<bool> ForgetAsync(CyrodiilContext db, long knower, long known, CancellationToken ct) =>
        await db.LoreIntroductions.Where(i => i.KnowerCharacterId == knower && i.KnownCharacterId == known).ExecuteDeleteAsync(ct) > 0;
}
