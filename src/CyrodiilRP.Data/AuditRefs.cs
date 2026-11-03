using System.Diagnostics.CodeAnalysis;

namespace CyrodiilRP.Data;

// Who an audit entry names: "account:<readym id>", "character:12" or "server".
public static class AuditRefs
{
    private const string AccountPrefix = "account:";
    private const string CharacterPrefix = "character:";

    public static string FormatAccount(Guid readyMId) => $"{AccountPrefix}{readyMId}";

    public static string FormatCharacter(long id) => $"{CharacterPrefix}{id}";

    public static bool TryAccount([NotNullWhen(true)] string? reference, out Guid readyMId)
    {
        readyMId = Guid.Empty;
        return reference is not null && reference.StartsWith(AccountPrefix, StringComparison.Ordinal)
                                     && Guid.TryParse(reference.AsSpan(AccountPrefix.Length), out readyMId);
    }

    public static bool TryCharacter([NotNullWhen(true)] string? reference, out long id)
    {
        id = 0;
        return reference is not null && reference.StartsWith(CharacterPrefix, StringComparison.Ordinal)
                                     && long.TryParse(reference.AsSpan(CharacterPrefix.Length), out id);
    }
}
