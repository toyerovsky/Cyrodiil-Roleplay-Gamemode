using CyrodiilRP.Server.Characters;
using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Server.Welcome;

internal static class WelcomeCards
{
    // specialization: the character's, in the player's language, null before the first choice.
    public static WelcomeView Build(OnlinePlayer player, string? specialization)
    {
        List<WelcomeCharacterView> characters = player.Character is { } character
            ? [new WelcomeCharacterView(character.FirstName, character.LastName, character.LastPlayedAt.ToUnixTimeMilliseconds(), character.PlayedSeconds,
                specialization)]
            : [];
        var empty = Math.Max(0, CharacterSlots.Open - characters.Count);
        return new WelcomeView(player.AccountName, player.PreviousVisitAt is null, characters, empty, CharacterSlots.Locked);
    }
}
