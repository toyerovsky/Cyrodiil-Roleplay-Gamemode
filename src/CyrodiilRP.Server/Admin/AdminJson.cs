using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using CyrodiilRP.Server.Faction;

namespace CyrodiilRP.Server.Admin;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AdminView))]
[JsonSerializable(typeof(AdminCardView))]
[JsonSerializable(typeof(List<AdminSanctionView>))]
[JsonSerializable(typeof(AdminAuditView))]
[JsonSerializable(typeof(AdminAuditFilter))]
[JsonSerializable(typeof(AdminRollFilter))]
[JsonSerializable(typeof(AdminRollsView))]
[JsonSerializable(typeof(AdminLoreView))]
[JsonSerializable(typeof(AdminItemFilter))]
[JsonSerializable(typeof(List<AdminItemView>))]
[JsonSerializable(typeof(List<StaffFactionView>))]
[JsonSerializable(typeof(StaffFactionLookupView))]
[JsonSerializable(typeof(List<AdminNoticeView>))]
internal sealed partial class AdminJson : JsonSerializerContext
{
    // A whole lore card in letters as they are: in escapes six bytes long one written in Polish would not fit a message.
    // Options built here do not take the context's naming policy.
    public static AdminJson Letters { get; } = new(new JsonSerializerOptions
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    });
}
