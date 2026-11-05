namespace CyrodiilRP.Data;

internal static class ColumnLengths
{
    // The name rules allow 20, the column leaves them room to grow.
    public const int NamePart = 40;

    public const int NameKey = 2 * NamePart + 1;

    // The game holds a nickname in 256 bytes.
    public const int Nickname = 256;

    public const int Locale = 8;

    public const int EnumName = 32;

    public const int RoleName = 32;

    public const int RoleLabel = 64;

    public const int ConditionKey = 64;

    public const int AuditAction = 64;

    public const int Reference = 64;

    public const int Reason = 300;

    public const int AuditDetails = 1000;

    // The game's area and cell ids are at most 255 bytes.
    public const int WorldId = 256;

    public const int ShopName = 32;

    public const int DiceSet = 32;

    // Five dice at most: "1d6 + 1d10 + 1d20 + 1d100" and the like.
    public const int DiceAsked = 64;

    // "100:100," five times.
    public const int DiceValues = 64;

    // A roll's reason is cut to 40 where it is made.
    public const int DiceReason = 64;

    public const int LoreFieldId = 32;

    // The catalog's longest id is 48.
    public const int LoreCatalogId = 64;

    // The longest field of the card, a long text.
    public const int LoreText = 2000;

    // A roll and its one reroll: "5d20:20,20,20,20,20=100" twice.
    public const int LoreRolls = 128;

    // Never between the two halves of a surrogate pair, which PostgreSQL refuses to store.
    public static string Cut(string value, int length)
    {
        if (value.Length <= length)
            return value;

        var end = char.IsHighSurrogate(value[length - 1]) ? length - 1 : length;
        return value[..end];
    }
}
