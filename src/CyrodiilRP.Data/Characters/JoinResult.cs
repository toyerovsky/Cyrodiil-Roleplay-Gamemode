namespace CyrodiilRP.Data.Characters;

public sealed record JoinResult(
    long AccountId,
    DateTimeOffset AccountCreatedAt,
    // When the account was last here before this join, null on its first.
    DateTimeOffset? PreviousVisitAt,
    string Locale,
    string[] Roles,
    DateTimeOffset? BannedUntil,
    string? BanReason,
    CharacterSnapshot? Character,
    IReadOnlyList<CharacterCondition> Conditions);
