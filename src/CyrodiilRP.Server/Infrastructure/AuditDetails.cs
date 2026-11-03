using System.Globalization;
using System.Reflection;
using System.Text;

namespace CyrodiilRP.Server.Infrastructure;

// new SanctionDetails(30, "spam") is written "minutes=30, reason=spam".
internal static class AuditDetails
{
    public static string? Format(object? details)
    {
        if (details is null or string)
            return (string?)details;

        var text = new StringBuilder();
        foreach (var property in details.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetValue(details) is not { } value)
                continue;

            if (text.Length > 0)
                text.Append(", ");

            text.Append(char.ToLowerInvariant(property.Name[0])).Append(property.Name.AsSpan(1)).Append('=')
                .Append(value is IFormattable formattable ? formattable.ToString(null, CultureInfo.InvariantCulture) : value.ToString());
        }

        return text.ToString();
    }
}
