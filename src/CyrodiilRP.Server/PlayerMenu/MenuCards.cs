using CyrodiilRP.Server.Characters;
using CyrodiilRP.Server.Conditions;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Server.PlayerMenu;

internal static class MenuCards
{
    // The database's count of the time played and the time since it was last written.
    public static MenuCharacterView BuildCharacter(OnlinePlayer player, CharacterRecord character, int? gold, DateTimeOffset now)
    {
        var unwritten = Math.Max(0, (long)(now - player.PlayCountedUntil).TotalSeconds);
        return new MenuCharacterView(
            character.FirstName, character.LastName, ToMilliseconds(character.CreatedAt), character.PlayedSeconds + unwritten, gold);
    }

    // The sanctions soonest over first, those held until lifted last.
    public static MenuAccountView BuildAccount(OnlinePlayer player, IEnumerable<TitledCondition> conditions, Func<Phrase, string> text)
    {
        var sanctions = conditions
            .OrderBy(titled => titled.Condition.ExpiresAt ?? DateTimeOffset.MaxValue)
            .Select(titled => new MenuSanctionView(text(titled.Title), titled.Condition.Reason, ToMilliseconds(titled.Condition.ExpiresAt)))
            .ToList();
        return new MenuAccountView(player.AccountName, ToMilliseconds(player.AccountCreatedAt), ToMilliseconds(player.PreviousVisitAt), sanctions);
    }

    private static long ToMilliseconds(DateTimeOffset at) => at.ToUnixTimeMilliseconds();

    private static long? ToMilliseconds(DateTimeOffset? at) => at?.ToUnixTimeMilliseconds();
}
