using System.Globalization;
using OblivionMP.SDK.Serverside.Items;
using ReadyM.Relay.Common.Oblivion;
using ReadyM.Relay.Common.Oblivion.ECS.Values;
using ReadyM.SDK.Attributes;

namespace Roleplay.Jobs.Server;

/// <summary>The item a config names: a form id in hex, or "custom:" and the key of a custom item defined by
/// <see cref="WoodcuttingService"/>.</summary>
[Service]
public sealed partial class JobItemResolver(ICustomItems customItems)
{
    private const string CustomItemPrefix = "custom:";

    /// <summary>The item, or false while a custom item is not made yet (the first update after start).</summary>
    public bool TryResolve(string text, out NetworkFormId item)
    {
        if (!text.StartsWith(CustomItemPrefix, StringComparison.Ordinal))
        {
            item = new FormId(ParseFormId(text));
            return true;
        }
        return customItems.TryGet(text[CustomItemPrefix.Length..], out item);
    }

    public static uint ParseFormId(string text)
    {
        var digits = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? text[2..] : text;
        if (!uint.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            throw new FormatException($"Jobs config: '{text}' is not a form id. Write it in hex, such as 0x00019172.");
        return value;
    }
}
