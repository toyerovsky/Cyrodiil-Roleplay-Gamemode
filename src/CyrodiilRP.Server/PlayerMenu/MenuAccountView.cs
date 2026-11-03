using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.PlayerMenu;

// Times in Unix milliseconds, the previous visit null on the account's first.
internal sealed record MenuAccountView(
    [property: JsonPropertyName("nickname")] string Nickname,
    [property: JsonPropertyName("createdAt")] long CreatedAt,
    [property: JsonPropertyName("previousVisitAt")] long? PreviousVisitAt,
    [property: JsonPropertyName("sanctions")] IReadOnlyList<MenuSanctionView> Sanctions);
