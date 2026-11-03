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

    // Never between the two halves of a surrogate pair, which PostgreSQL refuses to store.
    public static string Cut(string value, int length)
    {
        if (value.Length <= length)
            return value;

        var end = char.IsHighSurrogate(value[length - 1]) ? length - 1 : length;
        return value[..end];
    }
}
