using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Admin;

[JsonSerializable(typeof(AdminView))]
[JsonSerializable(typeof(AdminCardView))]
[JsonSerializable(typeof(List<AdminSanctionView>))]
[JsonSerializable(typeof(AdminAuditView))]
[JsonSerializable(typeof(AdminAuditFilter))]
[JsonSerializable(typeof(AdminRollFilter))]
[JsonSerializable(typeof(AdminRollsView))]
[JsonSerializable(typeof(AdminItemFilter))]
[JsonSerializable(typeof(List<AdminItemView>))]
internal sealed partial class AdminJson : JsonSerializerContext;
